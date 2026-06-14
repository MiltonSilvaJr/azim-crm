using OpportunityPipeline.Domain.Opportunities.Exceptions;

namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Probabilidade de fechamento em percentual inteiro [0, 100].
/// Invariante INV-9: probability ∈ [0, 100].
/// Mapeia: Req 7, INV-9, design §4.3.
/// </summary>
public sealed record Probability
{
    /// <summary>Valor percentual no intervalo [0, 100].</summary>
    public int Value { get; }

    /// <summary>
    /// Cria instância com validação do intervalo.
    /// </summary>
    /// <param name="value">Percentual [0, 100].</param>
    /// <exception cref="DomainException">Se fora do intervalo [0, 100] (OP-ERR-007).</exception>
    public Probability(int value)
    {
        if (value < 0 || value > 100)
            throw new DomainException(
                $"Probabilidade deve estar no intervalo [0, 100]. Valor recebido: {value} (OP-ERR-007).");
        Value = value;
    }

    /// <summary>Probabilidade zero (sem chance de fechamento).</summary>
    public static Probability Zero => new(0);

    /// <summary>Probabilidade plena (100% de chance).</summary>
    public static Probability Full => new(100);

    /// <summary>Retorna representação legível para debug.</summary>
    public override string ToString() => $"{Value}%";
}
