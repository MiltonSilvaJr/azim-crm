using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Services;

/// <summary>
/// Resultado do cálculo de forecast líquido.
/// Todos os valores em centavos. Imutável.
/// Mapeia: Req 13, PBT-06, design §4.6.
/// </summary>
public sealed record NetForecastResult(
    Money ForecastPonderado,
    Money ComissaoPonderada,
    Money ForecastLiquido);
