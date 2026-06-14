namespace OpportunityPipeline.Domain.Opportunities.Events;

/// <summary>
/// Evento emitido após encerramento da oportunidade como perdida.
/// Mapeia: Req 10, design §4.4, AsyncAPI opportunity.lost.v1.
/// </summary>
public sealed record OpportunityLost(
    Guid EventId,
    Guid TenantId,
    Guid OpportunityId,
    Guid LossReasonId,
    DateTimeOffset ClosedAt,
    Guid ActorId,
    DateTimeOffset OccurredAt)
    : DomainEvent(EventId, TenantId, OpportunityId, OccurredAt)
{
    /// <inheritdoc/>
    public override string EventType => "opportunity.lost.v1";
}
