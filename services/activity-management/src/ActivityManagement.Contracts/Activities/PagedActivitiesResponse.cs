namespace ActivityManagement.Contracts.Activities;

/// <summary>
/// DTO de resposta paginada para listagem de atividades (Req 13, design §5.2).
/// Mapeia: design §8, TASK-18.
/// </summary>
/// <param name="Items">Atividades da página corrente.</param>
/// <param name="Total">Total de registros que correspondem ao filtro.</param>
/// <param name="Page">Página atual (base 1).</param>
/// <param name="PageSize">Tamanho da página.</param>
public sealed record PagedActivitiesResponse(
    IReadOnlyList<ActivityResponse> Items,
    int                             Total,
    int                             Page,
    int                             PageSize);
