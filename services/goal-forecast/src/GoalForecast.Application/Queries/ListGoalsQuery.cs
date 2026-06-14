using GoalForecast.Application.Behaviors;
using GoalForecast.Application.Common;
using GoalForecast.Application.Ports;
using GoalForecast.Domain.Authorization;
using MediatR;

namespace GoalForecast.Application.Queries;

/// <summary>
/// Query para listar metas do tenant com filtro RBAC por escopo de visibilidade.
/// Vendedor: apenas owner_id próprio; Gestor: sua BU; Admin/Executivo: tenant inteiro.
///
/// Mapeia: Req 3, Req 12, RNF 2, design §5.2, TASK-11.
/// </summary>
public sealed record ListGoalsQuery : IRequest<PagedResult<GoalDto>>, IHasPrincipal
{
    /// <summary>Principal autenticado (tenant_id, role, bu_id).</summary>
    public required GoalPrincipal Principal { get; init; }

    /// <summary>Filtro por BU (opcional).</summary>
    public Guid? BuId { get; init; }

    /// <summary>Filtro por responsável (opcional).</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>Filtro por ano (opcional).</summary>
    public int? Year { get; init; }

    /// <summary>Filtro por mês (opcional).</summary>
    public int? Month { get; init; }

    /// <summary>Número da página (≥ 1, default 1).</summary>
    public int Page { get; init; } = 1;

    /// <summary>Tamanho da página (1..200, default 50).</summary>
    public int PageSize { get; init; } = 50;
}
