using AccountManagement.Domain.Accounts.Exceptions;

namespace AccountManagement.Domain.Accounts.ValueObjects;

/// <summary>
/// Objeto de valor que representa o nome de uma conta.
///
/// Invariantes:
/// - Não pode ser nulo, vazio nem somente espaços após trim.
/// - Comprimento máximo de <see cref="MaxLength"/> caracteres.
/// - Igualdade por valor (semântica de value object).
///
/// Mapeia: design §4.3, Req 1.5, ACC-ERR-001.
/// </summary>
public sealed class AccountName : IEquatable<AccountName>
{
    /// <summary>Comprimento máximo permitido para o nome da conta.</summary>
    public const int MaxLength = 255;

    /// <summary>Valor do nome da conta.</summary>
    public string Value { get; }

    private AccountName(string value) => Value = value;

    /// <summary>
    /// Cria um <see cref="AccountName"/> validado.
    /// </summary>
    /// <param name="value">Nome da conta.</param>
    /// <returns>Instância validada de <see cref="AccountName"/>.</returns>
    /// <exception cref="AccountNameRequiredException">
    /// Lançada quando <paramref name="value"/> é nulo, vazio, somente espaços
    /// ou excede <see cref="MaxLength"/>.
    /// </exception>
    public static AccountName Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new AccountNameRequiredException("O nome da conta é obrigatório.");

        if (value.Length > MaxLength)
            throw new AccountNameRequiredException(
                $"O nome da conta não pode exceder {MaxLength} caracteres.");

        return new AccountName(value);
    }

    /// <inheritdoc />
    public bool Equals(AccountName? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AccountName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Compara dois <see cref="AccountName"/> por valor.</summary>
    public static bool operator ==(AccountName? left, AccountName? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Compara dois <see cref="AccountName"/> por desigualdade de valor.</summary>
    public static bool operator !=(AccountName? left, AccountName? right) => !(left == right);
}
