namespace OpportunityPipeline.Domain.Opportunities.Events;

/// <summary>
/// Evento emitido após congelamento do snapshot de comissão (Freeze ao ganhar).
/// Integração financeira de alta prioridade.
/// Mapeia: Req 14, design §4.4, AsyncAPI commission.snapshot_created.v1.
/// </summary>
public sealed record CommissionSnapshotCreated(
    Guid EventId,
    Guid TenantId,
    Guid OpportunityId,
    Guid PartnerId,
    Guid CommissionId,
    long ComissaoTotalCents,
    DateTimeOffset SnapshotAt,
    DateTimeOffset OccurredAt)
    : DomainEvent(EventId, TenantId, OpportunityId, OccurredAt)
{
    /// <inheritdoc/>
    public override string EventType => "commission.snapshot_created.v1";
}
