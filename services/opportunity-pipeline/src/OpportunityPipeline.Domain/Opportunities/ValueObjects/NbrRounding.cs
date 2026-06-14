namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Arredondamento monetário conforme NBR 5891 (ToEven / Banker's Rounding).
/// Todas as divisões monetárias do domínio devem usar este serviço.
/// Nunca usar float/double. Entrada e saída em centavos (long).
/// Mapeia: RNF 11, design §4.6, TASK-02.
/// </summary>
public static class NbrRounding
{
    /// <summary>
    /// Calcula round(numerator / denominator) usando MidpointRounding.ToEven (NBR 5891).
    /// Entrada e saída em centavos inteiros.
    /// </summary>
    /// <param name="numerator">Numerador (ex.: valor_total × probabilidade).</param>
    /// <param name="denominator">Denominador (ex.: 100 para percentuais).</param>
    /// <returns>Resultado arredondado em centavos.</returns>
    /// <exception cref="ArgumentException">Se denominator for zero.</exception>
    public static long RoundHalfToEven(long numerator, long denominator)
    {
        if (denominator == 0)
            throw new ArgumentException("O denominador não pode ser zero.", nameof(denominator));

        // Usa decimal para precisão intermediária (apenas como cálculo), resultado é long
        var exact = (decimal)numerator / denominator;
        return (long)Math.Round(exact, MidpointRounding.ToEven);
    }
}
