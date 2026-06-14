namespace PartnerManagement.Domain.Partners.ValueObjects;

/// <summary>
/// Estado do ciclo de vida de um parceiro.
/// Transições são idempotentes (DD-006, PBT-02):
/// inativar parceiro já inativo não altera estado nem emite evento de transição.
/// Mapeia: Req 3, Req 8, design §4.5.
/// </summary>
public enum PartnerStatus
{
    /// <summary>Parceiro ativo — elegível à vinculação com oportunidades.</summary>
    Active = 1,

    /// <summary>
    /// Parceiro inativo — soft-delete lógico. Não pode ser vinculado a novas oportunidades
    /// (Req 8). Vínculos existentes não são alterados.
    /// </summary>
    Inactive = 2
}
