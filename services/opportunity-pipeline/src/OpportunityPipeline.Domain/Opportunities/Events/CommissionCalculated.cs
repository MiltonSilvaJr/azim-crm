namespace OpportunityPipeline.Domain.Opportunities.Events;

/// <summary>
/// Evento emitido após cálculo/atualização de comissão projetada (SetPartnerCommission).
/// Mapeia: Req 11, design §4.4, AsyncAPI commission.calculated.v1.
/// </summary>
public sealed record CommissionCalculated(
    Guid EventId,
    Guid TenantId,
    Guid OpportunityId,
    Guid PartnerId,
    long ComissaoTotalCents,
    bool IsSnapshot,
    DateTimeOffset OccurredAt)
    : DomainEvent(EventId, TenantId, OpportunityId, OccurredAt)
{
    /// <inheritdoc/>
    public override string EventType => "commission.calculated.v1";
}
