namespace GoalForecast.Application.Ports;

/// <summary>
/// Parâmetros de filtro para consulta de metas via <see cref="IGoalRepository.Query"/>.
/// Todos os campos são opcionais; ausência = sem filtro naquele campo.
/// Paginação obrigatória: page ≥ 1, pageSize ∈ [1..200].
/// Mapeia: Req 3, design §5.2, §8.3.
/// </summary>
public sealed record GoalQueryFilter(
    Guid TenantId,
    Guid? BuId = null,
    Guid? OwnerId = null,
    int? Year = null,
    int? Month = null,
    int Page = 1,
    int PageSize = 50);

/// <summary>
/// Resultado paginado de consulta de metas.
/// </summary>
/// <typeparam name="T">Tipo dos itens retornados.</typeparam>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total);
