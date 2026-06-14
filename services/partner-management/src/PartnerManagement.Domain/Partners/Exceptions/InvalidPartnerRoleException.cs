namespace PartnerManagement.Domain.Partners.Exceptions;

/// <summary>
/// Lançada quando o papel do parceiro é inválido (nulo, vazio ou fora da lista canônica do tenant).
/// Mapeia: Req 5.2, Req 5.3, design §4.3, PM-ERR-002.
/// </summary>
public sealed class InvalidPartnerRoleException : DomainException
{
    /// <summary>
    /// Inicializa uma nova instância de <see cref="InvalidPartnerRoleException"/>.
    /// </summary>
    /// <param name="role">Papel informado que originou a exceção (não exposto em mensagens públicas).</param>
    public InvalidPartnerRoleException(string? role = null)
        : base("PM-ERR-002", "Papel de parceiro inválido.")
    {
        // role recebido não é exposto na mensagem (RNF 4, anti-enumeração)
        _ = role;
    }
}
