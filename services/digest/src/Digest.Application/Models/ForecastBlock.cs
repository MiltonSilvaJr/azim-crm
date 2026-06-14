using Digest.Domain.ValueObjects;

namespace Digest.Application.Models;

/// <summary>
/// Bloco de metas retornado por <see cref="Ports.IForecastReadPort"/>.
/// Contém os valores consolidados do período para composição do azimute (Req 5.3–5.5, DD-010).
/// Quando não há meta cadastrada, o port retorna <see langword="null"/> (degradação graciosa — Req 5.4, PBT-04).
/// Todos os valores em centavos (<see cref="MoneyCents"/>).
/// </summary>
/// <param name="PipelineWeighted">Valor ponderado total do pipeline em centavos.</param>
/// <param name="PipelineVariation">Variação do pipeline em relação ao período anterior em centavos.</param>
/// <param name="RevenueRealized">Receita realizada no período em centavos.</param>
/// <param name="RevenueGoal">Meta de receita do período em centavos.</param>
/// <param name="WonCount">Número de oportunidades ganhas no período.</param>
/// <param name="LostCount">Número de oportunidades perdidas no período.</param>
public sealed record ForecastBlock(
    MoneyCents PipelineWeighted,
    MoneyCents PipelineVariation,
    MoneyCents RevenueRealized,
    MoneyCents RevenueGoal,
    int WonCount,
    int LostCount);
