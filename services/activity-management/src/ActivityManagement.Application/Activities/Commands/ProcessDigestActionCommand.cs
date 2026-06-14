namespace ActivityManagement.Application.Activities.Commands;

using ActivityManagement.Application.Behaviors;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities.Repositories;
using ActivityManagement.Domain.Activities.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

// ── Exceções de token ────────────────────────────────────────────────────────

/// <summary>
/// Token inexistente, malformado ou inacessível (ACT-ERR-008, anti-enumeração).
/// A resposta deve ser indistinguível de atividade inacessível (Req 7.6, PBT-03).
/// </summary>
public sealed class InvalidDigestTokenException : Exception
{
    /// <summary>Inicializa a exceção de token inválido.</summary>
    public InvalidDigestTokenException()
        : base("Link inválido ou inacessível.")
    {
    }
}

/// <summary>
/// Token expirado (ACT-ERR-009 — HTTP 410, MSG-030).
/// </summary>
public sealed class ExpiredDigestTokenException : Exception
{
    /// <summary>Inicializa a exceção de token expirado.</summary>
    public ExpiredDigestTokenException()
        : base("Link expirado. Acesse o portal para visualizar a atividade.")
    {
    }
}

// ── Command e Handler ────────────────────────────────────────────────────────

/// <summary>
/// Resultado do processamento da ação do digest.
/// </summary>
/// <param name="ActivityId">Atividade processada.</param>
/// <param name="Action">Ação executada: <c>complete</c> ou <c>reschedule</c>.</param>
/// <param name="WasAlreadyProcessed">Verdadeiro quando token já havia sido consumido (idempotência).</param>
public sealed record DigestActionResult(
    Guid    ActivityId,
    string  Action,
    bool    WasAlreadyProcessed);

/// <summary>
/// Command para processamento da ação de 1 clique via link do digest (Req 7, Req 8.5).
/// Fluxo:
///   1. Localiza token por hash (anti-enumeração — PBT-03).
///   2. Token inválido/inacessível → ACT-ERR-008 (indistinguível em forma de atividade inacessível).
///   3. Token expirado → ACT-ERR-009 (HTTP 410).
///   4. Token já usado → retorna 200 idempotente (MSG-029, DD-004).
///   5. Token válido → processa ação (<c>complete</c> ou <c>reschedule</c>), marca <c>used_at</c>
///      na mesma transação (atomicidade — Req 7.3).
/// Não implementa <see cref="ITenantRequest"/> — autoridade vem do token (design §10).
/// Mapeia: design §5.1, Req 7, Req 8.5, RNF 3, RNF 5, DD-003, DD-004, PBT-02, PBT-03, TASK-09.
/// </summary>
/// <param name="TokenHash">Hash do token opaco apresentado pelo usuário no link do digest.</param>
/// <param name="NewDueAt">Nova data de vencimento; obrigatório quando ação for <c>reschedule</c>.</param>
public sealed record ProcessDigestActionCommand(string TokenHash, DateTimeOffset? NewDueAt = null)
    : IRequest<DigestActionResult>, ITransactionalCommand
{
    /// <inheritdoc />
    public IReadOnlyList<Domain.Activities.Events.DomainEvent> DomainEvents { get; private set; } = [];

    internal void SetDomainEvents(IReadOnlyList<Domain.Activities.Events.DomainEvent> events)
        => DomainEvents = events;
}

/// <summary>
/// Handler do <see cref="ProcessDigestActionCommand"/>.
/// Mapeia: design §5.1/§5.3, TASK-09.
/// </summary>
internal sealed class ProcessDigestActionCommandHandler
    : IRequestHandler<ProcessDigestActionCommand, DigestActionResult>
{
    private readonly IDigestActionTokenPort _tokenPort;
    private readonly IActivityRepository   _repository;
    private readonly IAuditPublisher        _auditPublisher;
    private readonly IClock                 _clock;
    private readonly IActivityMetrics       _metrics;
    private readonly ILogger<ProcessDigestActionCommandHandler> _logger;

    public ProcessDigestActionCommandHandler(
        IDigestActionTokenPort tokenPort,
        IActivityRepository    repository,
        IAuditPublisher        auditPublisher,
        IClock                 clock,
        IActivityMetrics       metrics,
        ILogger<ProcessDigestActionCommandHandler> logger)
    {
        _tokenPort      = tokenPort;
        _repository     = repository;
        _auditPublisher = auditPublisher;
        _clock          = clock;
        _metrics        = metrics;
        _logger         = logger;
    }

    public async Task<DigestActionResult> Handle(
        ProcessDigestActionCommand request,
        CancellationToken          cancellationToken)
    {
        var now = _clock.UtcNow;

        // (1) Localiza token — null para inexistente/malformado/inacessível (anti-enumeração)
        var token = await _tokenPort.FindByHashAsync(request.TokenHash, cancellationToken);

        if (token is null)
            throw new InvalidDigestTokenException(); // ACT-ERR-008

        // (2) Expirado
        if (token.ExpiresAt <= now)
        {
            _metrics.IncrementDigestTokenExpired();
            _logger.LogWarning(
                "Token expirado expires_at={ExpiresAt}",
                token.ExpiresAt);
            throw new ExpiredDigestTokenException(); // ACT-ERR-009
        }

        // (3) Já usado → retorna sucesso idempotente (MSG-029, DD-004)
        if (token.UsedAt.HasValue)
        {
            return new DigestActionResult(
                ActivityId:         token.ActivityId ?? Guid.Empty,
                Action:             token.Action,
                WasAlreadyProcessed: true);
        }

        // (4) Token válido → localiza atividade
        if (token.ActivityId is null)
            throw new InvalidDigestTokenException(); // token sem atividade vinculada → ACT-ERR-008

        var activity = await _repository.FindByIdAsync(token.ActivityId.Value, cancellationToken)
            ?? throw new InvalidDigestTokenException(); // atividade inacessível → ACT-ERR-008 (anti-enumeração)

        // (5) Executa ação
        switch (token.Action.ToLowerInvariant())
        {
            case "complete":
                activity.Complete(now, correlationId: Guid.NewGuid());
                break;

            case "reschedule":
                if (!request.NewDueAt.HasValue)
                    throw new InvalidOperationException("Nova data de vencimento é obrigatória para reagendamento.");
                activity.Reschedule(DueDate.Create(request.NewDueAt.Value), now);
                break;

            default:
                throw new InvalidDigestTokenException();
        }

        // (6) Persiste atividade + marca token used_at na mesma transação (atomicidade — Req 7.3)
        await _repository.SaveAsync(activity, cancellationToken);
        await _tokenPort.MarkUsedAsync(token.Id, now, cancellationToken);
        request.SetDomainEvents(activity.DomainEvents);

        // (7) Auditoria: correlaciona token↔atividade (Req 7.8, RNF 2.4)
        await _auditPublisher.PublishAsync(new AuditEntry(
            TenantId:      token.TenantId,
            UserId:        null, // ação via token — sem JWT de usuário
            EntityType:    "Activity",
            EntityId:      activity.Id,
            Action:        token.Action,
            DeltaJson:     "{}",
            CorrelationId: Guid.NewGuid()), cancellationToken);

        // Métrica: token usado com sucesso (RNF 6.2, design §11)
        _metrics.IncrementDigestTokenUsed();

        _logger.LogInformation(
            "Token de digest processado activity_id={ActivityId} action={Action}",
            activity.Id,
            token.Action);

        return new DigestActionResult(
            ActivityId:          activity.Id,
            Action:              token.Action,
            WasAlreadyProcessed: false);
    }
}
