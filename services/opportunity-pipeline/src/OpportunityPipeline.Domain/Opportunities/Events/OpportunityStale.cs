namespace OpportunityPipeline.Domain.Opportunities.Events;

/// <summary>
/// Evento emitido quando a oportunidade é marcada como estagnada (MarkStale).
/// Mapeia: Req 17, design §4.4, AsyncAPI opportunity.stale.v1.
/// </summary>
public sealed record OpportunityStale(
    Guid EventId,
    Guid TenantId,
    Guid OpportunityId,
    Guid BuId,
    DateTimeOffset LastActivityAt,
    DateTimeOffset DetectedAt,
    DateOnly DetectionPeriod)
    : DomainEvent(EventId, TenantId, OpportunityId, DetectedAt)
{
    /// <inheritdoc/>
    public override string EventType => "opportunity.stale.v1";
}
