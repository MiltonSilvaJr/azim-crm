using GoalForecast.Domain.Exceptions;

namespace GoalForecast.Domain.ValueObjects;

/// <summary>
/// Objeto de valor imutável que representa um valor monetário em centavos inteiros.
/// Proíbe uso de ponto flutuante (float/double/decimal) em toda manipulação monetária.
/// Operações aritméticas retornam novas instâncias; receptor nunca é modificado.
/// Igualdade por valor (record semantics).
///
/// Mapeia: Req 1.2, RNF 4, PBT-05, DEC-011, design §4.3, rule money-as-cents.md.
/// </summary>
/// <param name="Cents">Valor em centavos inteiros. Deve ser não-negativo.</param>
public sealed record Money(long Cents)
{
    /// <summary>Representação de zero centavos.</summary>
    public static readonly Money Zero = new(0L);

    /// <summary>
    /// Factory que constrói um <see cref="Money"/> a partir de centavos inteiros.
    /// Lança <see cref="DomainException"/> (GF-ERR-001) se <paramref name="cents"/> for negativo.
    /// Construção via double/float não é fornecida por design (RNF 4, design §4.3).
    /// </summary>
    /// <param name="cents">Valor em centavos inteiros não-negativo.</param>
    /// <returns>Instância imutável de <see cref="Money"/>.</returns>
    /// <exception cref="DomainException">GF-ERR-001 quando <paramref name="cents"/> é negativo.</exception>
    public static Money Of(long cents)
    {
        if (cents < 0L)
            throw new DomainException("GF-ERR-001",
                $"Valor de meta inválido: centavos não pode ser negativo (recebido: {cents}).");

        return new Money(cents);
    }

    /// <summary>
    /// Soma dois valores monetários e retorna nova instância.
    /// Ambos os operandos devem ser não-negativos (invariante mantida por construção).
    /// </summary>
    /// <param name="other">Valor a somar.</param>
    /// <returns>Nova instância com a soma exata.</returns>
    public Money Add(Money other) => new(Cents + other.Cents);

    /// <summary>
    /// Subtrai <paramref name="other"/> deste valor e retorna nova instância.
    /// Lança <see cref="DomainException"/> (GF-ERR-001) se o resultado for negativo.
    /// </summary>
    /// <param name="other">Valor a subtrair.</param>
    /// <returns>Nova instância com a diferença exata.</returns>
    /// <exception cref="DomainException">GF-ERR-001 quando resultado seria negativo.</exception>
    public Money Subtract(Money other)
    {
        var result = Cents - other.Cents;
        if (result < 0L)
            throw new DomainException("GF-ERR-001",
                $"Valor de meta inválido: subtração resultaria em centavos negativos " +
                $"({Cents} - {other.Cents} = {result}).");

        return new Money(result);
    }

    /// <inheritdoc/>
    public override string ToString() => $"Money({Cents} cents)";
}
