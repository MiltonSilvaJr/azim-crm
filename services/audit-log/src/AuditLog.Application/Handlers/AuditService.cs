using System.Diagnostics;
using AuditLog.Application.Abstractions;
using AuditLog.Application.Commands;
using AuditLog.Domain.Abstractions;
using AuditLog.Domain.Aggregates;
using AuditLog.Domain.Repositories;
using AuditLog.Domain.Services;
using AuditLog.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuditLog.Application.Handlers;

/// <summary>
/// Handler de <see cref="RecordAuditEntryCommand"/>.
/// Implementa a recepção centralizada de auditoria (REQ-006):
/// mascara PII → constrói aggregate → persiste na transação corrente (DD-001, fail-closed).
/// <para>Instrumentado com métricas OpenTelemetry (design §11.2, §11.3).</para>
/// </summary>
public sealed class AuditService : IRequestHandler<RecordAuditEntryCommand>
{
    /// <summary>ActivitySource para traces do AuditService (design §11.3).</summary>
    public static readonly ActivitySource ActivitySource = new("AuditLog.AuditService", "1.0.0");

    private readonly IAuditLogRepository _repository;
    private readonly PiiMasker _piiMasker;
    private readonly IPiiFieldPolicy _piiFieldPolicy;
    private readonly IClock _clock;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditMetrics _metrics;
    private readonly ILogger<AuditService> _logger;

    /// <summary>Inicializa o handler com suas dependências.</summary>
    public AuditService(
        IAuditLogRepository repository,
        PiiMasker piiMasker,
        IPiiFieldPolicy piiFieldPolicy,
        IClock clock,
        ITenantContext tenantContext,
        IAuditMetrics metrics,
        ILogger<AuditService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(piiMasker);
        ArgumentNullException.ThrowIfNull(piiFieldPolicy);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _piiMasker = piiMasker;
        _piiFieldPolicy = piiFieldPolicy;
        _clock = clock;
        _tenantContext = tenantContext;
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Sequência obrigatória (design §5.3):
    /// 1. Incrementa contador de eventos recebidos.
    /// 2. Resolve TenantId do contexto autenticado.
    /// 3. Constrói AuditDelta a partir dos dados brutos.
    /// 4. Mascara PII no delta (REQ-004) antes de qualquer persistência.
    /// 5. Constrói o aggregate AuditLogAggregate com created_at via IClock (REQ-002.4).
    /// 6. Persiste via repositório na transação corrente (DD-001).
    /// 7. Registra métricas de latência e PII (design §11.2).
    /// </remarks>
    public async Task Handle(RecordAuditEntryCommand request, CancellationToken cancellationToken)
    {
        _metrics.IncrementEventsReceived();

        // Span de trace cobrindo mascaramento + INSERT (design §11.3)
        using var activity = ActivitySource.StartActivity("AuditService.Record");
        activity?.SetTag("audit.entity_type", request.EntityType);
        activity?.SetTag("audit.action", request.Action.ToString());

        var tenantIdValue = _tenantContext.TenantId
            ?? throw new InvalidOperationException(
                "TenantId ausente no contexto ao processar RecordAuditEntryCommand. " +
                "Verifique o TenantContextBehavior na pipeline.");

        var tenantId = TenantId.From(tenantIdValue);
        var actorId = ActorId.From(request.ActorId);
        var entityRef = EntityReference.Create(request.EntityType, request.EntityId);

        // Constrói delta bruto a partir dos dados recebidos
        var rawDelta = BuildDelta(request.Action, request.RawBefore, request.RawAfter);

        // Verifica se mascaramento será aplicado antes de chamar o masker
        var hasPiiFields = _piiFieldPolicy.GetPiiFields(request.EntityType).Count > 0;

        // Mascara PII antes de qualquer persistência (REQ-004 / DD-004)
        var maskedDelta = _piiMasker.Mask(request.EntityType, rawDelta);

        // Incrementa contador de mascaramento se campos PII foram processados (design §11.2)
        if (hasPiiFields)
            _metrics.IncrementPiiMaskingApplied();

        // Constrói o aggregate — created_at derivado do IClock (REQ-002.4)
        var auditLog = AuditLogAggregate.Create(
            tenantId,
            actorId,
            entityRef,
            request.Action,
            maskedDelta,
            _clock);

        // Mede a latência do INSERT (design §11.2, RNF-003)
        var sw = Stopwatch.StartNew();
        try
        {
            await _repository.AddAsync(auditLog, cancellationToken);
            sw.Stop();
            _metrics.RecordInsertLatency(sw.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _metrics.RecordInsertLatency(sw.Elapsed.TotalSeconds);
            _metrics.IncrementInsertFailures();

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Loga a falha sem expor PII (RNF-002.3): apenas metadados não sensíveis
            _logger.LogError(ex,
                "Falha ao persistir registro de auditoria. EntityType={EntityType} EntityId={EntityId} Action={Action}",
                request.EntityType,
                request.EntityId,
                request.Action);

            throw; // fail-closed (DD-001): deixa a transação de negócio ser revertida
        }
    }

    // ------------------------------------------------------------------ Delta building

    private static AuditDelta BuildDelta(
        AuditAction action,
        IReadOnlyDictionary<string, object?>? rawBefore,
        IReadOnlyDictionary<string, object?>? rawAfter)
    {
        return action switch
        {
            AuditAction.Create => AuditDelta.ForCreate(rawAfter!),
            AuditAction.Delete => AuditDelta.ForDelete(rawBefore!),
            AuditAction.Update => AuditDelta.ForUpdate(ComputeDiff(rawBefore!, rawAfter!)),
            _ => throw new InvalidOperationException($"AuditAction desconhecida: {action}.")
        };
    }

    /// <summary>
    /// Computa o diff campo a campo entre estados anterior e posterior.
    /// Retorna apenas os campos com mudança efetiva (REQ-003.4).
    /// </summary>
    private static IReadOnlyDictionary<string, AuditAttributeChange> ComputeDiff(
        IReadOnlyDictionary<string, object?> before,
        IReadOnlyDictionary<string, object?> after)
    {
        var allKeys = before.Keys.Union(after.Keys, StringComparer.Ordinal);
        var changes = new Dictionary<string, AuditAttributeChange>(StringComparer.Ordinal);

        foreach (var key in allKeys)
        {
            before.TryGetValue(key, out var beforeVal);
            after.TryGetValue(key, out var afterVal);

            if (!Equals(beforeVal, afterVal))
                changes[key] = new AuditAttributeChange(beforeVal, afterVal);
        }

        return changes.AsReadOnly();
    }
}
