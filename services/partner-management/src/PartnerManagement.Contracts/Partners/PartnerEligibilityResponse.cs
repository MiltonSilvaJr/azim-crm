namespace PartnerManagement.Contracts.Partners;

/// <summary>
/// DTO de response para o endpoint de elegibilidade de parceiro.
/// Consumido internamente pelo opportunity-pipeline via mTLS (Req 8.1, DD-007).
/// Mapeia: design §8, Req 8, TASK-22.
/// </summary>
public sealed class PartnerEligibilityResponse
{
    /// <summary>Identificador do parceiro.</summary>
    public Guid PartnerId { get; init; }

    /// <summary>
    /// Verdadeiro se o parceiro está ativo e elegível para vinculação a novas oportunidades.
    /// Falso bloqueia a vinculação no pipeline (PM-ERR-005 retornado pelo pipeline, não por este módulo).
    /// </summary>
    public bool Active { get; init; }
}
