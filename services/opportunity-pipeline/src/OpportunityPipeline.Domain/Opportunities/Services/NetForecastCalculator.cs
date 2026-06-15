using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Services;

/// <summary>
/// Serviço de domínio puro: calcula forecast líquido descontando comissão ponderada.
/// Fórmulas:
///   forecast_ponderado = round(valor_total × probabilidade / 100)
///   comissao_ponderada = round(comissao_total × probabilidade / 100)
///   forecast_liquido = forecast_ponderado − comissao_ponderada
/// Sem comissão: forecast_liquido = forecast_ponderado.
/// Mapeia: Req 13, PBT-06, design §4.6.
/// </summary>
public static class NetForecastCalculator
{
    /// <summary>
    /// Calcula o forecast líquido descontando a comissão ponderada.
    /// </summary>
    /// <param name="contractValue">Valor contratual.</param>
    /// <param name="probability">Probabilidade de fechamento.</param>
    /// <param name="commissionCalculation">Resultado do cálculo de comissão.</param>
    /// <returns>Resultado com forecast ponderado, comissão ponderada e forecast líquido.</returns>
    public static NetForecastResult Calculate(
        ContractValue contractValue,
        Probability probability,
        CommissionCalculation commissionCalculation)
    {
        ArgumentNullException.ThrowIfNull(contractValue);
        ArgumentNullException.ThrowIfNull(probability);
        ArgumentNullException.ThrowIfNull(commissionCalculation);

        var forecastPonderado = ForecastCalculator.Calculate(contractValue, probability);

        // comissao_ponderada = round(comissao_total × prob / 100)
        var comissaoPonderadaCents = NbrRounding.RoundHalfToEven(
            commissionCalculation.ComissaoTotal.AmountInCents * probability.Value,
            100);
        // Herda a moeda do contrato (ADR-0008)
        var currency = contractValue.Currency;
        var comissaoPonderada = new Money(comissaoPonderadaCents, currency);

        // forecast_liquido = forecast_ponderado − comissao_ponderada
        // Garante não-negativo: comissao_ponderada ≤ forecast_ponderado por construção matemática
        var forecastLiquidoCents = Math.Max(0L,
            forecastPonderado.AmountInCents - comissaoPonderada.AmountInCents);
        var forecastLiquido = new Money(forecastLiquidoCents, currency);

        return new NetForecastResult(forecastPonderado, comissaoPonderada, forecastLiquido);
    }
}
