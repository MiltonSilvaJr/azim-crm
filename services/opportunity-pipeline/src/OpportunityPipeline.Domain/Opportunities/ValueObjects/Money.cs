using OpportunityPipeline.Domain.Opportunities.Exceptions;

namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Valor monetário representado em centavos inteiros (BRL).
/// Regras: não negativo; sem construtores float/double; aritmética inteira.
/// Arredondamento apenas via <see cref="NbrRounding"/> (NBR 5891 ToEven).
/// Mapeia: RNF 11, DD-004, design §4.3.
/// </summary>
public sealed record Money
{
    /// <summary>Valor em centavos inteiros. Sempre ≥ 0.</summary>
    public long AmountInCents { get; }

    /// <summary>
    /// Cria uma instância de Money com o valor em centavos.
    /// </summary>
    /// <param name="amountInCents">Valor em centavos. Deve ser ≥ 0.</param>
    /// <exception cref="DomainException">Se amountInCents for negativo.</exception>
    public Money(long amountInCents)
    {
        if (amountInCents < 0)
            throw new DomainException(
                $"Valor monetário não pode ser negativo. Valor recebido: {amountInCents} centavos.");
        AmountInCents = amountInCents;
    }

    /// <summary>Instância representando zero centavos.</summary>
    public static Money Zero => new(0L);

    /// <summary>Soma dois valores monetários.</summary>
    public Money Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Money(AmountInCents + other.AmountInCents);
    }

    /// <summary>Subtrai outro valor monetário. Resultado não pode ser negativo.</summary>
    /// <exception cref="DomainException">Se o resultado for negativo.</exception>
    public Money Subtract(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var result = AmountInCents - other.AmountInCents;
        if (result < 0)
            throw new DomainException(
                $"Subtração monetária resultaria em valor negativo: {AmountInCents} - {other.AmountInCents}.");
        return new Money(result);
    }

    /// <summary>Retorna representação legível para debug (ex.: "1590 centavos").</summary>
    public override string ToString() => $"{AmountInCents} centavos";
}
