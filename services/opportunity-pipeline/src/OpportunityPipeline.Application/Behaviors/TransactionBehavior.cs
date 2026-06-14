using MediatR;
using Microsoft.Extensions.Logging;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Domain.Opportunities.Ports;

namespace OpportunityPipeline.Application.Behaviors;

/// <summary>
/// Pipeline behavior: abre transação, executa handler e persiste Outbox + auditoria.
/// Executa SEXTO (último) — wraps o handler com atomicidade transacional.
/// Falha em qualquer passo provoca rollback total.
/// Mapeia: Req 20.4 (consistência transacional), RNF 5.4, design §5.4 posição 6.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork,
    IDomainEventDispatcher domainEventDispatcher,
    IAuditPublisher auditPublisher,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger,
    TenantContext tenantContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Queries não participam de transação explícita
        if (request is IQuery)
            return await next(cancellationToken).ConfigureAwait(false);

        logger.LogDebug(
            "TransactionBehavior: iniciando transação para {CommandType}",
            typeof(TRequest).Name);

        await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);

            // Publica eventos de domínio acumulados (Outbox — mesma tx)
            var pendingEvents = unitOfWork.PendingDomainEvents;
            if (pendingEvents.Count > 0)
                await domainEventDispatcher.DispatchAllAsync(pendingEvents, cancellationToken).ConfigureAwait(false);

            // Auditoria imutável (mesma tx)
            if (request is IAuthenticatedCommand authCmd && unitOfWork.HasPendingAudit)
            {
                await auditPublisher.PublishAsync(
                    tenantContext.TenantId,
                    unitOfWork.AuditAggregateId,
                    unitOfWork.AuditAggregateType,
                    typeof(TRequest).Name,
                    tenantContext.ActorId,
                    unitOfWork.AuditDelta,
                    cancellationToken).ConfigureAwait(false);
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            logger.LogDebug(
                "TransactionBehavior: transação confirmada para {CommandType}",
                typeof(TRequest).Name);

            return response;
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(cancellationToken).ConfigureAwait(false);

            logger.LogWarning(
                ex,
                "TransactionBehavior: rollback executado para {CommandType}. Motivo: {Error}",
                typeof(TRequest).Name,
                ex.Message);

            throw;
        }
    }
}

/// <summary>
/// Interface marcadora para queries — exclui do TransactionBehavior.
/// Mapeia: design §5.2 (queries são read-only, sem transação explícita).
/// </summary>
public interface IQuery { }

/// <summary>
/// Unidade de trabalho — coordena transação, eventos pendentes e metadados de auditoria.
/// Implementação na Infrastructure (EF Core DbContext).
/// Mapeia: design §5.3, Req 20.4, RNF 5.4.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Inicia transação de banco de dados.</summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Confirma transação.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Reverte transação (rollback total).</summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>Eventos de domínio acumulados durante o handler (para dispatch via Outbox).</summary>
    IReadOnlyList<Domain.Opportunities.Events.DomainEvent> PendingDomainEvents { get; }

    /// <summary>Acumula evento de domínio para dispatch no commit.</summary>
    void AddDomainEvent(Domain.Opportunities.Events.DomainEvent domainEvent);

    /// <summary>Acumula múltiplos eventos de domínio.</summary>
    void AddDomainEvents(IEnumerable<Domain.Opportunities.Events.DomainEvent> domainEvents);

    /// <summary>Indica se há registro de auditoria pendente para persistir.</summary>
    bool HasPendingAudit { get; }

    /// <summary>ID do agregado para auditoria.</summary>
    Guid AuditAggregateId { get; }

    /// <summary>Tipo do agregado para auditoria (ex.: "Opportunity").</summary>
    string AuditAggregateType { get; }

    /// <summary>Delta de dados para auditoria (mascarado de PII pelo AuditPublisher).</summary>
    object? AuditDelta { get; }

    /// <summary>Registra metadados de auditoria para persistência no commit.</summary>
    void RegisterAudit(Guid aggregateId, string aggregateType, object? delta);
}
