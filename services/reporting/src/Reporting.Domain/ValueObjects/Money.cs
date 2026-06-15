namespace Reporting.Domain.ValueObjects;

/// <summary>
/// Objeto de valor imutável que representa um montante monetário em centavos inteiros
/// com moeda ISO-4217 explícita (ADR-0008). Moedas suportadas: BRL, USD, EUR.
///
/// Invariantes:
/// <list type="bullet">
///   <item><description><see cref="Cents"/> é <c>long</c> não-negativo — sem <c>float</c>/<c>double</c>.</description></item>
///   <item><description><see cref="Currency"/> é código ISO-4217 de moeda suportada.</description></item>
///   <item><description>Soma e subtração são fechadas em inteiros sem perda de precisão (PBT-02).</description></item>
///   <item><description>Add/Subtract lançam <see cref="InvalidOperationException"/> em mismatch de moeda (ADR-0008).</description></item>
///   <item><description>Formatação é responsabilidade da camada de apresentação — nunca aqui.</description></item>
/// </list>
///
/// Mapeia: TASK-03, design §4.3, DD-007, ADR-0008, PBT-02.
/// </summary>
public sealed record Money
{
    /// <summary>Conjunto de moedas suportadas no MVP (ADR-0008).</summary>
    public static readonly IReadOnlySet<string> SupportedCurrencies =
        new HashSet<string>(StringComparer.Ordinal) { "BRL", "USD", "EUR" };

    /// <summary>
    /// Valor em centavos inteiros.
    /// Exemplo: R$ 100,00 → <c>10000</c>; R$ 0,01 → <c>1</c>.
    /// </summary>
    public long Cents { get; }

    /// <summary>
    /// Código ISO-4217 da moeda (BRL, USD ou EUR) (ADR-0008).
    /// </summary>
    public string Currency { get; }

    /// <summary>Instância zero em centavos em BRL (retrocompatibilidade).</summary>
    public static readonly Money Zero = new(0L, "BRL");

    private Money(long cents, string currency)
    {
        if (cents < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cents),
                $"O valor em cents deve ser não-negativo. Recebido: {cents}. (DD-007)");
        }

        if (string.IsNullOrWhiteSpace(currency) || !SupportedCurrencies.Contains(currency))
        {
            throw new ArgumentOutOfRangeException(nameof(currency),
                $"Moeda inválida: '{currency}'. Moedas suportadas: BRL, USD, EUR (ADR-0008).");
        }

        Cents = cents;
        Currency = currency;
    }

    /// <summary>
    /// Cria um <see cref="Money"/> a partir de centavos inteiros em BRL (retrocompatibilidade).
    /// </summary>
    /// <param name="cents">Valor em centavos (≥ 0).</param>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="cents"/> for negativo.</exception>
    public static Money FromCents(long cents) => new(cents, "BRL");

    /// <summary>
    /// Cria um <see cref="Money"/> a partir de centavos inteiros e moeda explícita (ADR-0008).
    /// </summary>
    /// <param name="cents">Valor em centavos (≥ 0).</param>
    /// <param name="currency">Código ISO-4217: BRL, USD ou EUR.</param>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="cents"/> for negativo ou moeda inválida.</exception>
    public static Money FromCents(long cents, string currency) => new(cents, currency);

    /// <summary>
    /// Retorna um novo <see cref="Money"/> com a soma dos dois valores.
    /// Moedas devem ser idênticas (ADR-0008).
    /// </summary>
    /// <param name="other">Outro montante a somar.</param>
    /// <returns>Novo objeto imutável com a soma.</returns>
    /// <exception cref="InvalidOperationException">Em mismatch de moeda (ADR-0008).</exception>
    public Money Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameCurrency(other);
        return new Money(Cents + other.Cents, Currency);
    }

    /// <summary>
    /// Retorna um novo <see cref="Money"/> com a diferença entre os dois valores.
    /// Moedas devem ser idênticas (ADR-0008).
    /// </summary>
    /// <param name="other">Montante a subtrair.</param>
    /// <exception cref="InvalidOperationException">Se o resultado for negativo ou moedas forem diferentes (ADR-0008).</exception>
    public Money Subtract(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameCurrency(other);
        var result = Cents - other.Cents;
        if (result < 0)
        {
            throw new InvalidOperationException(
                $"Subtração resultaria em valor negativo ({Cents} - {other.Cents} = {result}). (DD-007)");
        }

        return new Money(result, Currency);
    }

    /// <inheritdoc/>
    public override string ToString() => $"Money({Cents} cents {Currency})";

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Operação entre moedas diferentes não é permitida: {Currency} e {other.Currency} (ADR-0008).");
        }
    }
}
