namespace ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Objeto de valor imutável que referencia uma conta do mesmo tenant.
/// A existência e o ownership da conta são validados pela porta
/// <c>IAccountReadPort</c> na camada de Application (Req 3.3).
/// Mapeia: design §4.3, Req 3.1, Req 3.3, TASK-02.
/// </summary>
public sealed class AccountLink : IEquatable<AccountLink>
{
    private AccountLink(Guid accountId) => AccountId = accountId;

    /// <summary>Identificador UUID da conta vinculada.</summary>
    public Guid AccountId { get; }

    /// <summary>
    /// Cria um <see cref="AccountLink"/> a partir do identificador da conta.
    /// </summary>
    /// <param name="accountId">UUID da conta; não pode ser vazio.</param>
    /// <returns>Instância imutável de <see cref="AccountLink"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Lançada quando <paramref name="accountId"/> é <see cref="Guid.Empty"/>.
    /// </exception>
    public static AccountLink Create(Guid accountId)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException(
                "O identificador da conta não pode ser vazio.", nameof(accountId));
        return new AccountLink(accountId);
    }

    // ── Igualdade por valor ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool Equals(AccountLink? other) =>
        other is not null && AccountId == other.AccountId;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is AccountLink other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(AccountId);

    /// <summary>Igualdade estrutural entre dois objetos de valor.</summary>
    public static bool operator ==(AccountLink? left, AccountLink? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Desigualdade estrutural entre dois objetos de valor.</summary>
    public static bool operator !=(AccountLink? left, AccountLink? right) =>
        !(left == right);

    /// <inheritdoc/>
    public override string ToString() => AccountId.ToString();
}
