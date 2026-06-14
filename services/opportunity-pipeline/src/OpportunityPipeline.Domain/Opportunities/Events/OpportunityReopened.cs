using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Events;

/// <summary>
/// Evento emitido após reabertura de oportunidade (won|lost → open).
/// Mapeia: Req 15, design §4.4, AsyncAPI opportunity.reopened.v1.
/// </summary>
public sealed record OpportunityReopened(
    Guid EventId,
    Guid TenantId,
    Guid OpportunityId,
    StageCategory PreviousCategory,
    Guid ActorId,
    string? Reason,
    DateTimeOffset OccurredAt)
    : DomainEvent(EventId, TenantId, OpportunityId, OccurredAt)
{
    /// <inheritdoc/>
    public override string EventType => "opportunity.reopened.v1";
}
