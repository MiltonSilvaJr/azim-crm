namespace PartnerManagement.Domain.Partners.Exceptions;

/// <summary>
/// Lançada quando o nome do parceiro é nulo, vazio ou composto apenas de espaços em branco.
/// Mapeia: Req 1.1, design §4.3, PM-ERR-001.
/// </summary>
public sealed class PartnerNameRequiredException : DomainException
{
    /// <summary>Inicializa uma nova instância de <see cref="PartnerNameRequiredException"/>.</summary>
    public PartnerNameRequiredException()
        : base("PM-ERR-001", "Nome do parceiro é obrigatório.")
    {
    }
}
