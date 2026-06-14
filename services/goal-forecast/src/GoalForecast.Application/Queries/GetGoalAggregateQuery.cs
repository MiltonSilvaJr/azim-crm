using GoalForecast.Application.Behaviors;
using GoalForecast.Domain.Authorization;
using MediatR;

namespace GoalForecast.Application.Queries;

/// <summary>
/// Granularidade da agregação de metas: trimestral ou anual.
/// Mapeia: Req 7, design §5.2, §8.5, TASK-13.
/// </summary>
public enum AggregateGranularity
{
    /// <summary>Soma dos 3 meses do trimestre (Q1..Q4).</summary>
    Quarter,

    /// <summary>Soma dos 12 meses do ano.</summary>
    Year
}

/// <summary>
/// Query para agregação derivada de metas por trimestre ou ano.
/// Meses ausentes contribuem com zero (RN-027, PBT-02, DD-003).
/// Resultado em centavos inteiros (long).
///
/// Mapeia: Req 7, PBT-02, DD-003, RN-027, design §5.2, §8.5, TASK-13.
/// </summary>
public sealed record GetGoalAggregateQuery : IRequest<GoalAggregateResult>, IHasPrincipal
{
    /// <summary>Principal autenticado.</summary>
    public required GoalPrincipal Principal { get; init; }

    /// <summary>Identificador da BU.</summary>
    public required Guid BuId { get; init; }

    /// <summary>Identificador do responsável; nulo para escopo BU.</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>Ano de referência.</summary>
    public required int Year { get; init; }

    /// <summary>Granularidade da agregação: quarter ou year.</summary>
    public required AggregateGranularity Granularity { get; init; }

    /// <summary>Trimestre (1..4); obrigatório quando Granularity = Quarter.</summary>
    public int? Quarter { get; init; }
}

/// <summary>
/// Resultado da agregação derivada. Valor em centavos inteiros, nunca persistido (DD-003).
/// </summary>
public sealed record GoalAggregateResult(
    string Granularity,
    int Year,
    int? Quarter,
    long ValorMetaAgregado);
