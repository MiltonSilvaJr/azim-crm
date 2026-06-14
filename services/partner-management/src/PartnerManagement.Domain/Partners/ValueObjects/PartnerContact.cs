namespace PartnerManagement.Domain.Partners.ValueObjects;

/// <summary>
/// Objeto de valor que representa o contato opcional de um parceiro (e-mail e/ou telefone).
/// Expõe <see cref="ToMasked()"/> para uso seguro em logs e auditoria (RNF 4, DD-008).
/// <c>ToString()</c> nunca retorna dados de contato em claro.
/// Mapeia: Req 7, RNF 4, design §4.3.
/// </summary>
public sealed class PartnerContact : IEquatable<PartnerContact>
{
    /// <summary>E-mail de contato (possível PII).</summary>
    public Email? EmailAddress { get; }

    /// <summary>Telefone de contato (possível PII).</summary>
    public Phone? PhoneNumber { get; }

    private PartnerContact(Email? email, Phone? phone)
    {
        EmailAddress = email;
        PhoneNumber = phone;
    }

    /// <summary>
    /// Construtor sem parâmetros para rehidratação pelo EF Core (owned type).
    /// Não deve ser chamado diretamente — use <see cref="Create"/>.
    /// </summary>
#pragma warning disable CS8618
    private PartnerContact()
    {
    }
#pragma warning restore CS8618

    /// <summary>
    /// Cria um <see cref="PartnerContact"/> com os dados fornecidos.
    /// </summary>
    /// <param name="emailValue">Endereço de e-mail; validado por <see cref="Email.Create"/>.</param>
    /// <param name="phoneValue">Número de telefone; validado por <see cref="Phone.Create"/>.</param>
    /// <returns>Instância válida de <see cref="PartnerContact"/>.</returns>
    /// <exception cref="Exceptions.InvalidPartnerContactException">
    /// Lançada quando o e-mail informado é de formato inválido.
    /// </exception>
    public static PartnerContact Create(string? emailValue, string? phoneValue)
    {
        Email? email = emailValue is not null ? Email.Create(emailValue) : null;
        Phone? phone = phoneValue is not null ? Phone.Create(phoneValue) : null;
        return new PartnerContact(email, phone);
    }

    /// <summary>
    /// Retorna representação mascarada dos dados de contato para uso em logs e auditoria.
    /// Nunca retorna e-mail nem telefone em claro (RNF 4, DD-008).
    /// </summary>
    public string ToMasked()
    {
        string emailMasked = EmailAddress is not null ? "***@***" : "(sem e-mail)";
        string phoneMasked = PhoneNumber is not null ? "***" : "(sem telefone)";
        return $"email={emailMasked}, phone={phoneMasked}";
    }

    /// <summary>
    /// Retorna representação neutra — não expõe PII (RNF 4, DD-008).
    /// Use <see cref="ToMasked()"/> para contextos de log/auditoria.
    /// </summary>
    public override string ToString() => "[PartnerContact]";

    /// <inheritdoc/>
    public bool Equals(PartnerContact? other) =>
        other is not null &&
        EmailAddress == other.EmailAddress &&
        PhoneNumber == other.PhoneNumber;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PartnerContact other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(EmailAddress, PhoneNumber);

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(PartnerContact? left, PartnerContact? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(PartnerContact? left, PartnerContact? right) => !(left == right);
}
