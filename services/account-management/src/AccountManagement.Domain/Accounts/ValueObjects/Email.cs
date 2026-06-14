using System.Text.RegularExpressions;
using AccountManagement.Domain.Accounts.Exceptions;

namespace AccountManagement.Domain.Accounts.ValueObjects;

/// <summary>
/// Objeto de valor que representa o endereço de e-mail de um contato.
///
/// Invariantes:
/// - Formato válido conforme regex RFC simplificada.
/// - Normalizado para minúsculas (invariante de cultura).
/// - Igualdade por valor.
/// - Lança <see cref="InvalidEmailException"/> quando inválido.
///
/// Nota PII: não exponha o valor em logs, traces ou exceções. Use via
/// <see cref="ContactInfo.ToMasked"/> para auditoria (design §4.3, RNF 1, DD-003).
///
/// Mapeia: design §4.3, Req 5.4, ACC-ERR-004.
/// </summary>
public sealed class Email : IEquatable<Email>
{
    /// <summary>Regex de validação de e-mail (RFC simplificada).</summary>
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(100));

    /// <summary>Valor do e-mail normalizado para minúsculas.</summary>
    public string Value { get; }

    private Email(string value) => Value = value;

    /// <summary>
    /// Cria um <see cref="Email"/> validado e normalizado.
    /// </summary>
    /// <param name="value">Endereço de e-mail a validar.</param>
    /// <returns>Instância de <see cref="Email"/> normalizado para minúsculas.</returns>
    /// <exception cref="InvalidEmailException">
    /// Lançada quando <paramref name="value"/> é nulo, vazio ou em formato inválido.
    /// </exception>
    public static Email Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidEmailException("O endereço de e-mail não pode ser vazio.");

        var normalized = value.ToLowerInvariant();

        if (!EmailRegex.IsMatch(normalized))
            throw new InvalidEmailException("O endereço de e-mail está em formato inválido.");

        // Rejeitar duplo arroba após normalização
        if (normalized.Count(c => c == '@') != 1)
            throw new InvalidEmailException("O endereço de e-mail está em formato inválido.");

        return new Email(normalized);
    }

    /// <inheritdoc />
    public bool Equals(Email? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Email other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <inheritdoc />
    /// <remarks>
    /// Não use em logs diretamente — use <see cref="ContactInfo.ToMasked"/> (RNF 1).
    /// </remarks>
    public override string ToString() => Value;
}
