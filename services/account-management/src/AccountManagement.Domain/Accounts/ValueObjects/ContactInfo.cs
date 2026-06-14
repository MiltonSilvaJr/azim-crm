using AccountManagement.Domain.Accounts.Exceptions;

namespace AccountManagement.Domain.Accounts.ValueObjects;

/// <summary>
/// Objeto de valor que encapsula os dados de PII de um contato:
/// nome, e-mail e telefone.
///
/// Invariantes:
/// - Nome obrigatório (não nulo, não vazio).
/// - E-mail e telefone opcionais.
/// - Expõe representação mascarada via <see cref="ToMasked"/> para uso em
///   logs, auditoria e eventos de domínio (RNF 1, DD-003).
/// - Igualdade por valor.
///
/// Nota de segurança: esta classe contém PII. Nunca serialize diretamente para
/// logs, traces, payloads de eventos ou exceções. Use <see cref="ToMasked"/>.
///
/// Mapeia: design §4.3, Req 5.2, RNF 1, DD-003.
/// </summary>
public sealed class ContactInfo : IEquatable<ContactInfo>
{
    /// <summary>
    /// Marcador de anonimização usado em <see cref="ToMasked"/> e na operação
    /// de esquecimento LGPD (design §7, DD-001).
    /// </summary>
    public const string AnonymizationMarker = "[anonimizado]";

    /// <summary>Nome do contato (PII).</summary>
    public string Name { get; }

    /// <summary>E-mail do contato (PII); <c>null</c> quando não informado.</summary>
    public Email? Email { get; }

    /// <summary>Telefone do contato (PII); <c>null</c> quando não informado.</summary>
    public Phone? Phone { get; }

    private ContactInfo(string name, Email? email, Phone? phone)
    {
        Name = name;
        Email = email;
        Phone = phone;
    }

    /// <summary>
    /// Cria um <see cref="ContactInfo"/> validado.
    /// </summary>
    /// <param name="name">Nome do contato (obrigatório).</param>
    /// <param name="email">E-mail do contato (opcional).</param>
    /// <param name="phone">Telefone do contato (opcional).</param>
    /// <returns>Instância validada de <see cref="ContactInfo"/>.</returns>
    /// <exception cref="AccountNameRequiredException">
    /// Lançada quando <paramref name="name"/> é nulo, vazio ou somente espaços.
    /// </exception>
    public static ContactInfo Create(string? name, Email? email = null, Phone? phone = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new AccountNameRequiredException("O nome do contato é obrigatório.");

        return new ContactInfo(name, email, phone);
    }

    /// <summary>
    /// Retorna uma representação mascarada de <see cref="ContactInfo"/> sem PII em claro,
    /// segura para uso em logs, auditoria e payloads de eventos de domínio.
    ///
    /// O nome é substituído por <see cref="AnonymizationMarker"/>;
    /// e-mail e telefone são omitidos (<c>null</c>).
    ///
    /// Mapeia: design §4.3, RNF 1.1, RNF 1.2, DD-003.
    /// </summary>
    /// <returns>
    /// Instância de <see cref="ContactInfo"/> com <see cref="Name"/> mascarado
    /// e <see cref="Email"/>/<see cref="Phone"/> nulos.
    /// </returns>
    public ContactInfo ToMasked() =>
        new(AnonymizationMarker, email: null, phone: null);

    // =========================================================================
    // Igualdade por valor
    // =========================================================================

    /// <inheritdoc />
    public bool Equals(ContactInfo? other)
    {
        if (other is null) return false;
        return string.Equals(Name, other.Name, StringComparison.Ordinal)
               && Equals(Email, other.Email)
               && Equals(Phone, other.Phone);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ContactInfo other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(Name),
            Email?.GetHashCode() ?? 0,
            Phone?.GetHashCode() ?? 0);

    /// <inheritdoc />
    /// <remarks>
    /// Não use em logs diretamente. Use <see cref="ToMasked"/> (RNF 1).
    /// </remarks>
    public override string ToString() =>
        $"ContactInfo(Name=[REDACTED], HasEmail={Email is not null}, HasPhone={Phone is not null})";
}
