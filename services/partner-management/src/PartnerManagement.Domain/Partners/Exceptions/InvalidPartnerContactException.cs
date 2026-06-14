namespace PartnerManagement.Domain.Partners.Exceptions;

/// <summary>
/// Lançada quando o contato do parceiro contém dados inválidos (e-mail malformado ou telefone inválido).
/// Mapeia: Req 7.2, design §4.3, PM-ERR-004.
/// </summary>
public sealed class InvalidPartnerContactException : DomainException
{
    /// <summary>
    /// Inicializa uma nova instância de <see cref="InvalidPartnerContactException"/>.
    /// </summary>
    public InvalidPartnerContactException()
        : base("PM-ERR-004", "E-mail de contato inválido.")
    {
    }
}
