using System.Text.RegularExpressions;
using PartnerManagement.Domain.Partners.Exceptions;

namespace PartnerManagement.Domain.Partners.ValueObjects;

/// <summary>
/// Objeto de valor que representa um endereço de e-mail válido, normalizado para minúsculas.
/// Tratado como possível PII (DD-008, RNF 4).
/// Mapeia: Req 7.2, design §4.3.
/// </summary>
public sealed class Email : IEquatable<Email>
{
    /// <summary>
    /// Regex RFC simplificada para validação de e-mail.
    /// Garante formato local@dominio.tld. Constante testável.
    /// </summary>
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));

    /// <summary>Endereço de e-mail normalizado (minúsculas).</summary>
    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Cria um <see cref="Email"/> válido e normalizado para minúsculas.
    /// </summary>
    /// <param name="value">Endereço de e-mail a ser validado.</param>
    /// <returns>Instância válida de <see cref="Email"/>.</returns>
    /// <exception cref="InvalidPartnerContactException">
    /// Lançada quando o e-mail é nulo, vazio ou de formato inválido.
    /// </exception>
    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidPartnerContactException();
        }

        string normalized = value.Trim().ToLowerInvariant();

        if (!EmailRegex.IsMatch(normalized))
        {
            throw new InvalidPartnerContactException();
        }

        return new Email(normalized);
    }

    /// <inheritdoc/>
    public bool Equals(Email? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Email other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Value);

    /// <summary>Retorna representação mascarada — não expõe PII (RNF 4, DD-008).</summary>
    public override string ToString() => "[Email]";

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(Email? left, Email? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(Email? left, Email? right) => !(left == right);
}
