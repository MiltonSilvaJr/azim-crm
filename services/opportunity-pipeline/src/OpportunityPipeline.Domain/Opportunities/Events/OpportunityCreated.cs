namespace OpportunityPipeline.Domain.Opportunities.Events;

/// <summary>
/// Evento emitido após a criação bem-sucedida de uma oportunidade.
/// Sem PII — apenas IDs e metadados de negócio.
/// Mapeia: Req 1, design §4.4, AsyncAPI opportunity.created.v1.
/// </summary>
public sealed record OpportunityCreated(
    Guid EventId,
    Guid TenantId,
    Guid OpportunityId,
    string OpportunityNumber,
    Guid BuId,
    Guid AccountId,
    Guid OwnerId,
    Guid StageId,
    Guid OriginChannelId,
    Guid ActorId,
    DateTimeOffset OccurredAt)
    : DomainEvent(EventId, TenantId, OpportunityId, OccurredAt)
{
    /// <inheritdoc/>
    public override string EventType => "opportunity.created.v1";
}
