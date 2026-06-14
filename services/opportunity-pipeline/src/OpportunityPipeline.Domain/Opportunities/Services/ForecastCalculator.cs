using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Services;

/// <summary>
/// Serviço de domínio puro: calcula forecast_ponderado.
/// Fórmula: round(valor_total × probabilidade / 100) com NBR 5891 ToEven.
/// Sem dependências de infraestrutura. 100% determinístico.
/// Mapeia: Req 8, INV-8, PBT-04, design §4.6.
/// </summary>
public static class ForecastCalculator
{
    /// <summary>
    /// Calcula o forecast ponderado em centavos.
    /// </summary>
    /// <param name="contractValue">Valor contratual com TotalInCents calculado.</param>
    /// <param name="probability">Probabilidade de fechamento [0, 100].</param>
    /// <returns>Forecast ponderado em centavos (Money).</returns>
    public static Money Calculate(ContractValue contractValue, Probability probability)
    {
        ArgumentNullException.ThrowIfNull(contractValue);
        ArgumentNullException.ThrowIfNull(probability);

        var forecastCents = NbrRounding.RoundHalfToEven(
            contractValue.TotalInCents * probability.Value,
            100);

        return new Money(forecastCents);
    }
}
