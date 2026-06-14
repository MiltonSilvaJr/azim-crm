using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Events;

/// <summary>
/// Evento emitido após movimentação de estágio (MoveStage).
/// Mapeia: Req 5, design §4.4, AsyncAPI opportunity.stage_changed.v1.
/// </summary>
public sealed record OpportunityStageChanged(
    Guid EventId,
    Guid TenantId,
    Guid OpportunityId,
    Guid FromStageId,
    Guid ToStageId,
    StageCategory FromCategory,
    StageCategory ToCategory,
    Guid ActorId,
    DateTimeOffset OccurredAt)
    : DomainEvent(EventId, TenantId, OpportunityId, OccurredAt)
{
    /// <inheritdoc/>
    public override string EventType => "opportunity.stage_changed.v1";
}
