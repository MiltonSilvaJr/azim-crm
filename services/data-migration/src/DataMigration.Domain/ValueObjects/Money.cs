namespace DataMigration.Domain.ValueObjects;

/// <summary>
/// Objeto de valor monetário em centavos inteiros (<see cref="long"/>).
///
/// Regras (`.forge/rules/domain/money-as-cents.md`, design §4.3, Req 3.6):
///   - Armazenado como <c>long</c> (centavos, BIGINT no banco).
///   - Moeda padrão: BRL.
///   - Proibido <c>float</c>, <c>double</c> ou <c>decimal</c> em cálculo de domínio.
///   - Valor ≥ 0 (valores negativos não são representados neste contexto de import).
///
/// Imutável; igualdade por <see cref="AmountInCents"/> e <see cref="Currency"/>.
///
/// Rastreia: design §4.3, Req 3.6, TASK-07.
/// </summary>
public sealed class Money : IEquatable<Money>
{
    /// <summary>Valor monetário em centavos (ex: R$ 15,90 → 1590L).</summary>
    public long AmountInCents { get; }

    /// <summary>Moeda (ISO 4217). Padrão: BRL.</summary>
    public string Currency { get; }

    /// <summary>Representação de valor zero em BRL.</summary>
    public static Money Zero { get; } = new(0L, "BRL");

    private Money(long amountInCents, string currency)
    {
        AmountInCents = amountInCents;
        Currency = currency;
    }

    /// <summary>
    /// Cria um <see cref="Money"/> a partir de um valor em centavos.
    /// </summary>
    /// <param name="amountInCents">Valor em centavos (≥ 0).</param>
    /// <param name="currency">Moeda ISO 4217 (padrão: BRL).</param>
    /// <exception cref="ArgumentOutOfRangeException">Quando <paramref name="amountInCents"/> &lt; 0.</exception>
    public static Money OfCents(long amountInCents, string currency = "BRL")
    {
        if (amountInCents < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amountInCents),
                $"Valor em centavos deve ser ≥ 0. Recebido: {amountInCents}.");
        }

        return new Money(amountInCents, currency);
    }

    /// <inheritdoc />
    public bool Equals(Money? other)
    {
        if (other is null)
        {
            return false;
        }

        return AmountInCents == other.AmountInCents
            && string.Equals(Currency, other.Currency, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Money);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(AmountInCents, Currency);

    /// <inheritdoc />
    public override string ToString() => $"{AmountInCents} centavos ({Currency})";

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(Money? left, Money? right) => Equals(left, right);

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(Money? left, Money? right) => !Equals(left, right);
}
