namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Resultado imutável do cálculo de comissão por componente.
/// Todos os valores em centavos (long). Nunca float/double.
/// Mapeia: Req 12, design §4.3, PBT-05.
/// </summary>
public sealed record CommissionCalculation(
    Money ComissaoSetup,
    Money ComissaoRecorrente,
    Money ComissaoTotal);
