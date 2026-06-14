namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Snapshot leve do motivo de perda lido de organization.
/// Obrigatório ao encerrar como perdida (INV-7).
/// Mapeia: Req 10, INV-7, design §4.3.
/// </summary>
public sealed record LossReasonRef(
    Guid LossReasonId,
    string Name);
