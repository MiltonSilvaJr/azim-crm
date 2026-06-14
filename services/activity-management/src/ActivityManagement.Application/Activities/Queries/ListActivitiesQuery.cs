namespace ActivityManagement.Application.Activities.Queries;

using ActivityManagement.Application.Common;
using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Repositories;
using MediatR;

/// <summary>
/// Resultado paginado da listagem de atividades.
/// </summary>
/// <param name="Items">Atividades da página.</param>
/// <param name="Total">Total de registros que correspondem ao filtro.</param>
/// <param name="Page">Página atual (base 1).</param>
/// <param name="PageSize">Tamanho da página.</param>
public sealed record ListActivitiesResult(
    IReadOnlyList<Activity> Items,
    int                     Total,
    int                     Page,
    int                     PageSize);

/// <summary>
/// Query para listar atividades com filtros opcionais e paginação (Req 13, design §5.2).
/// Respeita o escopo do tenant autenticado.
/// Mapeia: design §5.2, Req 13, TASK-11.
/// </summary>
/// <param name="OwnerId">Filtrar por dono (opcional).</param>
/// <param name="Type">Filtrar por tipo (opcional).</param>
/// <param name="Status">Filtrar por status (opcional).</param>
/// <param name="OnlyOverdue">Retornar apenas vencidas (opcional).</param>
/// <param name="OpportunityId">Filtrar por oportunidade vinculada (opcional).</param>
/// <param name="Page">Página (base 1, padrão: 1).</param>
/// <param name="PageSize">Tamanho da página (padrão: 20).</param>
public sealed record ListActivitiesQuery(
    Guid?   OwnerId       = null,
    string? Type          = null,
    string? Status        = null,
    bool    OnlyOverdue   = false,
    Guid?   OpportunityId = null,
    int     Page          = 1,
    int     PageSize      = 20)
    : IRequest<ListActivitiesResult>, ITenantRequest
{
    /// <inheritdoc />
    public TenantContext? TenantContext { get; set; }
}

/// <summary>
/// Handler do <see cref="ListActivitiesQuery"/>.
/// </summary>
internal sealed class ListActivitiesQueryHandler : IRequestHandler<ListActivitiesQuery, ListActivitiesResult>
{
    private readonly IActivityRepository _repository;

    public ListActivitiesQueryHandler(IActivityRepository repository)
    {
        _repository = repository;
    }

    public async Task<ListActivitiesResult> Handle(
        ListActivitiesQuery query,
        CancellationToken   cancellationToken)
    {
        var filter = new ActivityListFilter(
            OwnerId:       query.OwnerId,
            Type:          query.Type,
            Status:        query.Status,
            OnlyOverdue:   query.OnlyOverdue,
            OpportunityId: query.OpportunityId,
            Page:          query.Page,
            PageSize:      query.PageSize);

        var (items, total) = await _repository.ListAsync(filter, cancellationToken);

        return new ListActivitiesResult(
            Items:    items,
            Total:    total,
            Page:     query.Page,
            PageSize: query.PageSize);
    }
}
