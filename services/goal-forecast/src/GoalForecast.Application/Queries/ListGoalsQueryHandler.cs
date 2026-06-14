using GoalForecast.Application.Common;
using GoalForecast.Application.Ports;
using GoalForecast.Domain.Aggregates;
using GoalForecast.Domain.Authorization;
using GoalForecast.Domain.Policies;
using GoalForecast.Domain.ValueObjects;
using MediatR;
using AppException = GoalForecast.Application.Common.ApplicationException;

namespace GoalForecast.Application.Queries;

/// <summary>
/// Handler que lista metas do tenant com filtro RBAC por escopo de visibilidade.
///
/// Regras RBAC (design §10, Req 12.3):
/// <list type="bullet">
///   <item>Vendedor: apenas metas onde é owner_id.</item>
///   <item>GestorDeBu: apenas metas da sua BU.</item>
///   <item>TenantAdmin / Executivo: todas as metas do tenant.</item>
/// </list>
///
/// Negação de autorização retorna 403 sem revelar existência de metas fora do escopo (RNF-2.3).
/// Filtro de tenant aplicado antes de qualquer outro (INV-4).
///
/// Mapeia: Req 3, Req 12, RNF 2, design §5.2, TASK-11.
/// </summary>
public sealed class ListGoalsQueryHandler
    : IRequestHandler<ListGoalsQuery, PagedResult<GoalDto>>
{
    private readonly IGoalRepository _repository;

    /// <summary>Inicializa com o repositório de metas.</summary>
    public ListGoalsQueryHandler(IGoalRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<GoalDto>> Handle(
        ListGoalsQuery query,
        CancellationToken cancellationToken)
    {
        var principal = query.Principal;
        var tenantId = principal.TenantId;

        // Limitar pageSize ao máximo permitido (design §8.3).
        var pageSize = Math.Min(query.PageSize, 200);
        var page = Math.Max(query.Page, 1);

        // Aplicar filtros RBAC: restringe o escopo de visibilidade antes da query.
        var (buIdFilter, ownerIdFilter) = ApplyRbacFilter(principal, query);

        var filter = new GoalQueryFilter(
            TenantId: tenantId,
            BuId: buIdFilter,
            OwnerId: ownerIdFilter,
            Year: query.Year,
            Month: query.Month,
            Page: page,
            PageSize: pageSize);

        var result = await _repository.Query(filter, cancellationToken);

        var dtos = result.Items.Select(MapToDto).ToList();
        return new PagedResult<GoalDto>(dtos, result.Page, result.PageSize, result.Total);
    }

    /// <summary>
    /// Aplica filtros RBAC ao escopo de visibilidade da query.
    /// GestorDeBu: força buId da BU do gestor.
    /// Vendedor: força ownerId do próprio vendedor.
    /// Admin/Executivo: permite filtros opcionais da query.
    /// </summary>
    private static (Guid? BuId, Guid? OwnerId) ApplyRbacFilter(
        GoalPrincipal principal,
        ListGoalsQuery query)
    {
        return principal.Role switch
        {
            GoalRole.Vendedor =>
                // Vendedor só vê metas onde é o owner_id.
                (query.BuId, OwnerId: principal.UserId),

            GoalRole.GestorDeBu =>
                // GestorDeBu vê apenas metas da sua BU.
                (BuId: principal.BuId ?? query.BuId, query.OwnerId),

            // TenantAdmin e Executivo: filtros opcionais da query.
            _ => (query.BuId, query.OwnerId)
        };
    }

    private static GoalDto MapToDto(Goal goal) =>
        new(
            Id: goal.Id,
            TenantId: goal.TenantId,
            Scope: goal.Scope.Kind.ToString(),
            BuId: goal.Scope.BuId,
            OwnerId: goal.Scope.OwnerId,
            Year: goal.Period.Year,
            Month: goal.Period.Month,
            ValorMeta: goal.ValorMeta.Cents,
            CreatedAt: goal.CreatedAt,
            UpdatedAt: goal.UpdatedAt);
}
