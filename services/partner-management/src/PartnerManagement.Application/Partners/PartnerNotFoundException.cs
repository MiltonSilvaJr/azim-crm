namespace PartnerManagement.Application.Partners;

/// <summary>
/// Exceção lançada quando um parceiro não é encontrado pelo identificador fornecido
/// ou está fora do tenant do contexto (PM-ERR-007, anti-enumeração RNF 1.4).
/// Mapeia: design §12.
/// </summary>
public sealed class PartnerNotFoundException : Exception
{
    /// <summary>Identificador do parceiro não encontrado.</summary>
    public Guid PartnerId { get; }

    /// <summary>
    /// Inicializa a exceção com o identificador do parceiro.
    /// </summary>
    public PartnerNotFoundException(Guid partnerId)
        : base($"Parceiro não encontrado: {partnerId}")
    {
        PartnerId = partnerId;
    }
}
