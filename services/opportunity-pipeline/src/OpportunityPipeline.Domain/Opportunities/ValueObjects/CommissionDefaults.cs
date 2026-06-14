namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Percentuais default de comissão lidos de partner-management.
/// Pré-preenche CommissionTerms no SetPartnerCommission (Req 11.2).
/// Mapeia: design §4.3.
/// </summary>
public sealed record CommissionDefaults(
    decimal PctSetup,
    decimal PctRecorrente);
