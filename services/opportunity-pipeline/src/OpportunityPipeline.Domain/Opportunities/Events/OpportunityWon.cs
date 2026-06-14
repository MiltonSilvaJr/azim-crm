namespace OpportunityPipeline.Domain.Opportunities.Events;

/// <summary>
/// Evento emitido após encerramento da oportunidade como ganha (com snapshot de comissão).
/// Mapeia: Req 14, design §4.4, AsyncAPI opportunity.won.v1.
/// </summary>
public sealed record OpportunityWon(
    Guid EventId,
    Guid TenantId,
    Guid OpportunityId,
    long ValorTotalCents,
    DateTimeOffset ClosedAt,
    Guid ActorId,
    DateTimeOffset OccurredAt)
    : DomainEvent(EventId, TenantId, OpportunityId, OccurredAt)
{
    /// <inheritdoc/>
    public override string EventType => "opportunity.won.v1";
}
