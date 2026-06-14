namespace Reporting.Domain.ValueObjects;

/// <summary>
/// Objeto de valor imutável que representa um montante monetário em centavos inteiros (BRL).
///
/// Invariantes:
/// <list type="bullet">
///   <item><description><see cref="Cents"/> é <c>long</c> não-negativo — sem <c>float</c>/<c>double</c>.</description></item>
///   <item><description>Soma e subtração são fechadas em inteiros sem perda de precisão (PBT-02).</description></item>
///   <item><description>Formatação em R$ é responsabilidade da camada de apresentação — nunca aqui.</description></item>
/// </list>
///
/// Mapeia: TASK-03, design §4.3, DD-007, PBT-02.
/// </summary>
public sealed record Money
{
    /// <summary>
    /// Valor em centavos inteiros.
    /// Exemplo: R$ 100,00 → <c>10000</c>; R$ 0,01 → <c>1</c>.
    /// </summary>
    public long Cents { get; }

    /// <summary>Instância zero em centavos.</summary>
    public static readonly Money Zero = new(0L);

    private Money(long cents)
    {
        if (cents < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cents),
                $"O valor em cents deve ser não-negativo. Recebido: {cents}. (DD-007)");
        }

        Cents = cents;
    }

    /// <summary>
    /// Cria um <see cref="Money"/> a partir de centavos inteiros.
    /// </summary>
    /// <param name="cents">Valor em centavos (≥ 0).</param>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="cents"/> for negativo.</exception>
    public static Money FromCents(long cents) => new(cents);

    /// <summary>
    /// Retorna um novo <see cref="Money"/> com a soma dos dois valores.
    /// </summary>
    /// <param name="other">Outro montante a somar.</param>
    /// <returns>Novo objeto imutável com a soma.</returns>
    public Money Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Money(Cents + other.Cents);
    }

    /// <summary>
    /// Retorna um novo <see cref="Money"/> com a diferença entre os dois valores.
    /// </summary>
    /// <param name="other">Montante a subtrair.</param>
    /// <exception cref="InvalidOperationException">Se o resultado for negativo.</exception>
    public Money Subtract(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var result = Cents - other.Cents;
        if (result < 0)
        {
            throw new InvalidOperationException(
                $"Subtração resultaria em valor negativo ({Cents} - {other.Cents} = {result}). (DD-007)");
        }

        return new Money(result);
    }

    /// <inheritdoc/>
    public override string ToString() => $"Money({Cents} cents)";
}
