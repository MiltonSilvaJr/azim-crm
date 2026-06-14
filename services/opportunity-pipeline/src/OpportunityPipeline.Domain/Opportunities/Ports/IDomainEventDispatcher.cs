using OpportunityPipeline.Domain.Opportunities.Events;

namespace OpportunityPipeline.Domain.Opportunities.Ports;

/// <summary>
/// Porta de despacho de eventos de domínio via Outbox (ADR-0004).
/// Implementação na Infrastructure persiste no outbox_events na mesma transação.
/// Mapeia: Req 20, design §6.6.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>Enfileira evento para publicação via Outbox.</summary>
    Task DispatchAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default);

    /// <summary>Enfileira múltiplos eventos de domínio.</summary>
    Task DispatchAllAsync(IEnumerable<DomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
