using GoalForecast.Application.Behaviors;
using GoalForecast.Domain.Authorization;
using MediatR;

namespace GoalForecast.Application.Queries;

/// <summary>
/// Query para o painel comparativo "Direção" (meta vs realizado vs pipeline).
/// Operação total: nunca retorna 404 nem NaN (PBT-04, DD-006, DD-007).
///
/// Mapeia: Req 5, Req 6, Req 8, DD-006, DD-007, PBT-03, PBT-04, design §5.2, TASK-12.
/// </summary>
public sealed record GetForecastPanelQuery : IRequest<ForecastPanelResult>, IHasPrincipal
{
    /// <summary>Principal autenticado.</summary>
    public required GoalPrincipal Principal { get; init; }

    /// <summary>Identificador da BU.</summary>
    public required Guid BuId { get; init; }

    /// <summary>Identificador do responsável; nulo para escopo BU.</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>Ano do período.</summary>
    public required int Year { get; init; }

    /// <summary>Mês do período (1..12).</summary>
    public required int Month { get; init; }
}

/// <summary>
/// Resultado do painel comparativo "Direção".
///
/// Semântica dos campos nulos (DD-006, DD-007):
/// <list type="bullet">
///   <item>ValorMeta/Gap/PctAtingimento = null → meta não cadastrada para o período.</item>
///   <item>Realizado/PipelineDisponivel = null quando PipelineUnavailable = true.</item>
///   <item>PipelineUnavailable = true → pipeline indisponível; sem zero confundível.</item>
/// </list>
///
/// Mapeia: Req 5, Req 6, RNF 6, DD-006, DD-007, design §8.4.
/// </summary>
public sealed record ForecastPanelResult(
    string Scope,
    Guid BuId,
    Guid? OwnerId,
    int Year,
    int Month,
    long? ValorMeta,
    long? Realizado,
    long? PipelineDisponivel,
    long? Gap,
    double? PctAtingimento,
    bool PipelineUnavailable);
