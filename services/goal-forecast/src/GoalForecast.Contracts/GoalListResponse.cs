namespace GoalForecast.Contracts;

/// <summary>
/// Resposta paginada de listagem de metas (GET /api/v1/goals).
/// Ordenação padrão: year, month, bu_id (design §8.3).
///
/// Mapeia: design §8.3, TASK-21, TASK-23.
/// </summary>
public sealed record GoalListResponse(
    /// <summary>Metas da página corrente.</summary>
    IReadOnlyList<GoalDto> Items,

    /// <summary>Número da página atual (≥ 1).</summary>
    int Page,

    /// <summary>Tamanho da página (1..200).</summary>
    int PageSize,

    /// <summary>Total de metas no filtro aplicado.</summary>
    int Total);
