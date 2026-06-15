using GoalForecast.Domain.Exceptions;

namespace GoalForecast.Domain.ValueObjects;

/// <summary>
/// Objeto de valor imutável que representa um valor monetário em centavos inteiros
/// com moeda ISO-4217 explícita (ADR-0008). Moedas suportadas: BRL, USD, EUR.
/// Proíbe uso de ponto flutuante (float/double/decimal) em toda manipulação monetária.
/// Operações aritméticas retornam novas instâncias; receptor nunca é modificado.
/// Add/Subtract lançam DomainException em mismatch de moeda (ADR-0008).
/// <see cref="Zero"/> é o valor zero em BRL (retrocompat); use <see cref="ZeroIn"/> para outras moedas.
///
/// Mapeia: Req 1.2, RNF 4, PBT-05, DEC-011, ADR-0008, design §4.3, rule money-as-cents.md.
/// </summary>
public sealed record Money
{
    /// <summary>Conjunto de moedas suportadas no MVP (ADR-0008).</summary>
    public static readonly IReadOnlySet<string> SupportedCurrencies =
        new HashSet<string>(StringComparer.Ordinal) { "BRL", "USD", "EUR" };

    /// <summary>
    /// Representação de zero centavos em BRL (retrocompatibilidade).
    /// Para outras moedas, use <see cref="ZeroIn(string)"/>.
    /// </summary>
    public static readonly Money Zero = new(0L, "BRL");

    /// <summary>Valor em centavos inteiros. Sempre não-negativo.</summary>
    public long Cents { get; }

    /// <summary>Código ISO-4217 da moeda (BRL, USD ou EUR).</summary>
    public string Currency { get; }

    /// <summary>
    /// Construtor principal com validação de invariantes (ADR-0008).
    /// </summary>
    /// <param name="cents">Valor em centavos inteiros não-negativo.</param>
    /// <param name="currency">Código ISO-4217: BRL, USD ou EUR.</param>
    /// <exception cref="DomainException">GF-ERR-001 quando cents negativo ou currency inválida.</exception>
    public Money(long cents, string currency)
    {
        if (cents < 0L)
            throw new DomainException("GF-ERR-001",
                $"Valor de meta inválido: centavos não pode ser negativo (recebido: {cents}).");

        if (string.IsNullOrWhiteSpace(currency) || !SupportedCurrencies.Contains(currency))
            throw new DomainException("GF-ERR-001",
                $"Moeda inválida: '{currency}'. Moedas suportadas: BRL, USD, EUR (ADR-0008).");

        Cents = cents;
        Currency = currency;
    }

    /// <summary>
    /// Cria instância representando zero centavos na moeda especificada (ADR-0008).
    /// </summary>
    /// <param name="currency">Código ISO-4217: BRL, USD ou EUR.</param>
    public static Money ZeroIn(string currency) => new(0L, currency);

    /// <summary>
    /// Factory que constrói um <see cref="Money"/> a partir de centavos inteiros em BRL (retrocompatibilidade).
    /// Lança <see cref="DomainException"/> (GF-ERR-001) se <paramref name="cents"/> for negativo.
    /// Para outras moedas, use <see cref="Of(long, string)"/>.
    /// </summary>
    /// <param name="cents">Valor em centavos inteiros não-negativo.</param>
    /// <returns>Instância imutável de <see cref="Money"/> em BRL.</returns>
    /// <exception cref="DomainException">GF-ERR-001 quando <paramref name="cents"/> é negativo.</exception>
    public static Money Of(long cents) => new(cents, "BRL");

    /// <summary>
    /// Factory que constrói um <see cref="Money"/> a partir de centavos inteiros e moeda explícita (ADR-0008).
    /// Lança <see cref="DomainException"/> (GF-ERR-001) se <paramref name="cents"/> for negativo
    /// ou <paramref name="currency"/> for inválida.
    /// </summary>
    /// <param name="cents">Valor em centavos inteiros não-negativo.</param>
    /// <param name="currency">Código ISO-4217: BRL, USD ou EUR.</param>
    /// <returns>Instância imutável de <see cref="Money"/>.</returns>
    /// <exception cref="DomainException">GF-ERR-001 quando cents negativo ou currency inválida.</exception>
    public static Money Of(long cents, string currency) => new(cents, currency);

    /// <summary>
    /// Soma dois valores monetários. Moedas devem ser idênticas (ADR-0008).
    /// </summary>
    /// <param name="other">Valor a somar.</param>
    /// <returns>Nova instância com a soma exata na mesma moeda.</returns>
    /// <exception cref="DomainException">GF-ERR-001 quando moedas são diferentes.</exception>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Cents + other.Cents, Currency);
    }

    /// <summary>
    /// Subtrai <paramref name="other"/> deste valor e retorna nova instância.
    /// Moedas devem ser idênticas (ADR-0008).
    /// Lança <see cref="DomainException"/> (GF-ERR-001) se o resultado for negativo.
    /// </summary>
    /// <param name="other">Valor a subtrair.</param>
    /// <returns>Nova instância com a diferença exata.</returns>
    /// <exception cref="DomainException">GF-ERR-001 quando moedas diferentes ou resultado negativo.</exception>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        var result = Cents - other.Cents;
        if (result < 0L)
            throw new DomainException("GF-ERR-001",
                $"Valor de meta inválido: subtração resultaria em centavos negativos " +
                $"({Cents} - {other.Cents} = {result}).");

        return new Money(result, Currency);
    }

    /// <inheritdoc/>
    public override string ToString() => $"Money({Cents} cents {Currency})";

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
            throw new DomainException("GF-ERR-001",
                $"Operação entre moedas diferentes não é permitida: {Currency} e {other.Currency} (ADR-0008).");
    }
}
