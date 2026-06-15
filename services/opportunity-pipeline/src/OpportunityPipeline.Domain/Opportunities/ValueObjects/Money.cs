using OpportunityPipeline.Domain.Opportunities.Exceptions;

namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Valor monetário representado em centavos inteiros com moeda ISO-4217 explícita (ADR-0008).
/// Moedas suportadas: BRL, USD, EUR.
/// Regras:
/// - <see cref="AmountInCents"/> não-negativo; sem construtores float/double.
/// - <see cref="Currency"/> deve ser BRL, USD ou EUR (validação no construtor).
/// - <see cref="Add"/> e <see cref="Subtract"/> lançam <see cref="DomainException"/> em
///   mismatch de moeda (proibido somar moedas diferentes — ADR-0008).
/// - Arredondamento apenas via NbrRounding (NBR 5891 ToEven).
/// Mapeia: ADR-0008, RNF 11, DD-004, design §4.3.
/// </summary>
public sealed record Money
{
    /// <summary>Conjunto de moedas suportadas no MVP (ADR-0008).</summary>
    public static readonly IReadOnlySet<string> SupportedCurrencies =
        new HashSet<string>(StringComparer.Ordinal) { "BRL", "USD", "EUR" };

    /// <summary>Valor em centavos inteiros. Sempre ≥ 0.</summary>
    public long AmountInCents { get; }

    /// <summary>Código ISO-4217 da moeda (BRL, USD ou EUR).</summary>
    public string Currency { get; }

    /// <summary>
    /// Cria uma instância de Money com valor em centavos e moeda explícita.
    /// </summary>
    /// <param name="amountInCents">Valor em centavos. Deve ser ≥ 0.</param>
    /// <param name="currency">Código ISO-4217: BRL, USD ou EUR.</param>
    /// <exception cref="DomainException">Se amountInCents negativo ou currency inválida.</exception>
    public Money(long amountInCents, string currency)
    {
        if (amountInCents < 0)
            throw new DomainException(
                $"Valor monetário não pode ser negativo. Valor recebido: {amountInCents} centavos.");

        if (string.IsNullOrWhiteSpace(currency) || !SupportedCurrencies.Contains(currency))
            throw new DomainException(
                $"Moeda inválida: '{currency}'. Moedas suportadas: BRL, USD, EUR (ADR-0008).");

        AmountInCents = amountInCents;
        Currency = currency;
    }

    /// <summary>
    /// Cria instância representando zero centavos na moeda especificada.
    /// </summary>
    /// <param name="currency">Código ISO-4217: BRL, USD ou EUR.</param>
    public static Money Zero(string currency) => new(0L, currency);

    /// <summary>
    /// Soma dois valores monetários. Moedas devem ser idênticas (ADR-0008).
    /// </summary>
    /// <exception cref="DomainException">Se as moedas forem diferentes.</exception>
    public Money Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameCurrency(other);
        return new Money(AmountInCents + other.AmountInCents, Currency);
    }

    /// <summary>
    /// Subtrai outro valor monetário. Resultado não pode ser negativo. Moedas devem ser idênticas.
    /// </summary>
    /// <exception cref="DomainException">Se as moedas forem diferentes ou resultado negativo.</exception>
    public Money Subtract(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameCurrency(other);
        var result = AmountInCents - other.AmountInCents;
        if (result < 0)
            throw new DomainException(
                $"Subtração monetária resultaria em valor negativo: {AmountInCents} - {other.AmountInCents} {Currency}.");
        return new Money(result, Currency);
    }

    /// <summary>Retorna representação legível para debug (ex.: "1590 centavos BRL").</summary>
    public override string ToString() => $"{AmountInCents} centavos {Currency}";

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
            throw new DomainException(
                $"Operação entre moedas diferentes não é permitida: {Currency} e {other.Currency} (ADR-0008).");
    }
}
