namespace OpportunityPipeline.Domain.Opportunities.Events;

/// <summary>
/// Base de todos os eventos de domínio do módulo opportunity-pipeline.
/// Imutável (record). Sem PII. Despachados via Outbox (ADR-0004).
/// Mapeia: Req 20, design §4.4.
/// </summary>
public abstract record DomainEvent(
    Guid EventId,
    Guid TenantId,
    Guid AggregateId,
    DateTimeOffset OccurredAt)
{
    /// <summary>Tipo do evento para roteamento no Outbox/Pub/Sub.</summary>
    public abstract string EventType { get; }
}
