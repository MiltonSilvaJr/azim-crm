namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Vínculo a um contato da conta. Não armazena PII — apenas o ID.
/// Exatamente 1 contato pode ser principal quando há ≥ 1 vínculo (INV-11).
/// Mapeia: Req 16, INV-11, design §4.3.
/// </summary>
public sealed record ContactLink(
    Guid ContactId,
    bool IsPrimary);
