namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Snapshot leve do canal de origem lido de organization.
/// IsPartnerChannel dispara a invariante INV-4 (parceiro obrigatório).
/// Mapeia: Req 4, INV-3, INV-4, design §4.3.
/// </summary>
public sealed record OriginChannelRef(
    Guid OriginChannelId,
    string Name,
    bool IsPartnerChannel);
