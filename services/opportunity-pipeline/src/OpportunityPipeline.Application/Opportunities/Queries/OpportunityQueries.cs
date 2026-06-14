using MediatR;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.Services;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;
using AppValidationException = OpportunityPipeline.Application.Common.ValidationException;

namespace OpportunityPipeline.Application.Opportunities.Queries;

// =========================================================================
// GetOpportunityQuery
// =========================================================================

/// <summary>
/// Query para detalhe de oportunidade (inclui flags is_overdue e is_stale derivadas).
/// Mapeia: Req 9.3, Req 17, design §5.2, TASK-12 ST-03.
/// </summary>
public sealed record GetOpportunityQuery(Guid OpportunityId) : IRequest<GetOpportunityResult>, IQuery, IAuthenticatedCommand
{
    public UserRole UserRole => UserRole.Viewer; // todas as roles podem ler
    public string CorrelationId => OpportunityId.ToString();
}

/// <summary>Resultado de GetOpportunityQuery.</summary>
public sealed record GetOpportunityResult(
    Guid Id,
    string Number,
    string Title,
    Guid OwnerId,
    Guid AccountId,
    Guid? PartnerId,
    string StageName,
    StageCategory StageCategory,
    long TotalValueCents,
    long ForecastPonderadoCents,
    bool IsStale,
    bool IsOverdue,
    DateOnly? ExpectedCloseDate,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Handler de GetOpportunityQuery.
/// Deriva is_overdue e is_stale via especificações de domínio (não persistidas).
/// Mapeia: design §5.2, TASK-12 ST-03.
/// </summary>
public sealed class GetOpportunityHandler(
    IOpportunityQueryRepository queryRepo,
    TenantContext tenantContext,
    IClock clock)
    : IRequestHandler<GetOpportunityQuery, GetOpportunityResult>
{
    public async Task<GetOpportunityResult> Handle(
        GetOpportunityQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;

        var opp = await queryRepo.GetByIdWithDetailsAsync(request.OpportunityId, tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new AppValidationException("opportunity_id", $"Oportunidade '{request.OpportunityId}' não encontrada.");

        var isOverdue = OverdueSpecification.IsSatisfiedBy(opp.StageCategory, opp.ExpectedCloseDate, clock.Today);
        var isStale = opp.IsStale;

        var forecastPonderado = ForecastCalculator.Calculate(opp.ContractValue, opp.Probability);

        return new GetOpportunityResult(
            opp.Id,
            opp.Number.Value,
            opp.Title,
            opp.OwnerId,
            opp.AccountId,
            opp.PartnerId,
            opp.Stage.Name,
            opp.StageCategory,
            opp.ContractValue.TotalInCents,
            forecastPonderado.AmountInCents,
            isStale,
            isOverdue,
            opp.ExpectedCloseDate,
            opp.ClosedAt,
            opp.CreatedAt,
            opp.UpdatedAt);
    }
}

// =========================================================================
// ListOpportunitiesQuery
// =========================================================================

/// <summary>
/// Query para listagem de oportunidades com filtros e paginação.
/// Mapeia: Req 19, design §5.2, TASK-12 ST-04.
/// </summary>
public sealed record ListOpportunitiesQuery : IRequest<PagedResult<OpportunitySummary>>, IQuery, IAuthenticatedCommand
{
    public UserRole UserRole => UserRole.Viewer;
    public string CorrelationId { get; init; } = string.Empty;

    public OpportunityFilter Filter { get; init; } = new();
    public PageRequest Page { get; init; } = new();
}

/// <summary>
/// Handler de ListOpportunitiesQuery.
/// Mapeia: design §5.2, TASK-12 ST-04.
/// </summary>
public sealed class ListOpportunitiesHandler(
    IOpportunityQueryRepository queryRepo,
    TenantContext tenantContext)
    : IRequestHandler<ListOpportunitiesQuery, PagedResult<OpportunitySummary>>
{
    public async Task<PagedResult<OpportunitySummary>> Handle(
        ListOpportunitiesQuery request,
        CancellationToken cancellationToken)
    {
        return await queryRepo.ListAsync(
            tenantContext.TenantId,
            tenantContext.BuId,
            request.Filter,
            request.Page,
            cancellationToken).ConfigureAwait(false);
    }
}

// =========================================================================
// GetKanbanQuery
// =========================================================================

/// <summary>
/// Query para kanban com SUM(valor_total) e SUM(forecast_ponderado) por estágio.
/// Paginação por coluna via stage_page_size.
/// Mapeia: Req 18, RNF 1, design §5.2, TASK-12 ST-05.
/// </summary>
public sealed record GetKanbanQuery : IRequest<IReadOnlyList<KanbanColumn>>, IQuery, IAuthenticatedCommand
{
    public UserRole UserRole => UserRole.Viewer;
    public string CorrelationId { get; init; } = string.Empty;
    public int StagePageSize { get; init; } = 20;
}

/// <summary>
/// Handler de GetKanbanQuery.
/// Delega agregação SQL ao repositório de query (performance — TASK-12 ST-05).
/// Mapeia: Req 18, design §5.2, TASK-12 ST-05.
/// </summary>
public sealed class GetKanbanHandler(
    IOpportunityQueryRepository queryRepo,
    TenantContext tenantContext)
    : IRequestHandler<GetKanbanQuery, IReadOnlyList<KanbanColumn>>
{
    public async Task<IReadOnlyList<KanbanColumn>> Handle(
        GetKanbanQuery request,
        CancellationToken cancellationToken)
    {
        return await queryRepo.GetKanbanAsync(
            tenantContext.TenantId,
            tenantContext.BuId,
            request.StagePageSize,
            cancellationToken).ConfigureAwait(false);
    }
}

// =========================================================================
// GetTimelineQuery
// =========================================================================

/// <summary>
/// Query para linha do tempo de transições de estágio (append-only).
/// Mapeia: RNF 7, design §5.2, TASK-12 ST-06.
/// </summary>
public sealed record GetTimelineQuery(Guid OpportunityId) : IRequest<IReadOnlyList<TimelineEntry>>, IQuery, IAuthenticatedCommand
{
    public UserRole UserRole => UserRole.Viewer;
    public string CorrelationId => OpportunityId.ToString();
}

/// <summary>Handler de GetTimelineQuery.</summary>
public sealed class GetTimelineHandler(
    IOpportunityQueryRepository queryRepo,
    TenantContext tenantContext)
    : IRequestHandler<GetTimelineQuery, IReadOnlyList<TimelineEntry>>
{
    public async Task<IReadOnlyList<TimelineEntry>> Handle(
        GetTimelineQuery request,
        CancellationToken cancellationToken)
    {
        return await queryRepo.GetTimelineAsync(
            tenantContext.TenantId,
            request.OpportunityId,
            cancellationToken).ConfigureAwait(false);
    }
}

// =========================================================================
// GetCommissionQuery
// =========================================================================

/// <summary>Resultado de GetCommissionQuery (inclui forecast_liquido).</summary>
public sealed record GetCommissionResult(
    IReadOnlyList<CommissionDetail> Commissions,
    long ForecastLiquidoCents);

/// <summary>
/// Query para detalhes de comissão, incluindo forecast_liquido via NetForecastCalculator.
/// Mapeia: Req 13, design §5.2, TASK-12 ST-06.
/// </summary>
public sealed record GetCommissionQuery(Guid OpportunityId) : IRequest<GetCommissionResult>, IQuery, IAuthenticatedCommand
{
    public UserRole UserRole => UserRole.Viewer;
    public string CorrelationId => OpportunityId.ToString();
}

/// <summary>
/// Handler de GetCommissionQuery.
/// Calcula forecast_liquido via NetForecastCalculator usando comissão ativa.
/// Mapeia: Req 13, design §5.2, TASK-12 ST-06.
/// </summary>
public sealed class GetCommissionHandler(
    IOpportunityQueryRepository queryRepo,
    TenantContext tenantContext)
    : IRequestHandler<GetCommissionQuery, GetCommissionResult>
{
    public async Task<GetCommissionResult> Handle(
        GetCommissionQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;

        var opp = await queryRepo.GetByIdWithDetailsAsync(request.OpportunityId, tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new AppValidationException("opportunity_id", $"Oportunidade '{request.OpportunityId}' não encontrada.");

        var commissions = await queryRepo.GetCommissionsAsync(tenantId, request.OpportunityId, cancellationToken).ConfigureAwait(false);

        // Calcula forecast_liquido usando comissão ativa (snapshot preferido sobre projetada)
        var activeCommission = opp.Commissions.FirstOrDefault(c => c.IsSnapshot)
            ?? opp.Commissions.FirstOrDefault(c => !c.IsSnapshot);

        long forecastLiquido = 0L;
        if (activeCommission is not null)
        {
            var netResult = NetForecastCalculator.Calculate(
                opp.ContractValue,
                opp.Probability,
                activeCommission.Calculation);
            forecastLiquido = netResult.ForecastLiquido.AmountInCents;
        }
        else
        {
            // Sem comissão: forecast_liquido = forecast_ponderado
            forecastLiquido = ForecastCalculator.Calculate(opp.ContractValue, opp.Probability).AmountInCents;
        }

        return new GetCommissionResult(commissions, forecastLiquido);
    }
}

// =========================================================================
// GetForecastQuery (interno)
// =========================================================================

/// <summary>Resultado de forecast agregado.</summary>
public sealed record GetForecastResult(
    long TotalValueCents,
    long ForecastPonderadoCents,
    long ForecastLiquidoCents);

/// <summary>
/// Query de forecast agregado para o tenant/BU (uso interno/relatórios).
/// Mapeia: Req 13, design §5.2, TASK-12 ST-06.
/// </summary>
public sealed record GetForecastQuery : IRequest<GetForecastResult>, IQuery, IAuthenticatedCommand
{
    public UserRole UserRole => UserRole.Viewer;
    public string CorrelationId { get; init; } = string.Empty;
}

/// <summary>Handler de GetForecastQuery.</summary>
public sealed class GetForecastHandler(
    IOpportunityQueryRepository queryRepo,
    TenantContext tenantContext)
    : IRequestHandler<GetForecastQuery, GetForecastResult>
{
    public async Task<GetForecastResult> Handle(
        GetForecastQuery request,
        CancellationToken cancellationToken)
    {
        var (totalValue, forecastPonderado, forecastLiquido) = await queryRepo.GetForecastAsync(
            tenantContext.TenantId,
            tenantContext.BuId,
            cancellationToken).ConfigureAwait(false);

        return new GetForecastResult(totalValue, forecastPonderado, forecastLiquido);
    }
}

// =========================================================================
// GetStaleQuery (interno)
// =========================================================================

/// <summary>
/// Query para oportunidades estagnadas (uso interno/alertas).
/// Mapeia: Req 17, design §5.2, TASK-12 ST-06.
/// </summary>
public sealed record GetStaleQuery : IRequest<PagedResult<OpportunitySummary>>, IQuery, IAuthenticatedCommand
{
    public UserRole UserRole => UserRole.Viewer;
    public string CorrelationId { get; init; } = string.Empty;
    public PageRequest Page { get; init; } = new();
}

/// <summary>Handler de GetStaleQuery.</summary>
public sealed class GetStaleHandler(
    IOpportunityQueryRepository queryRepo,
    TenantContext tenantContext)
    : IRequestHandler<GetStaleQuery, PagedResult<OpportunitySummary>>
{
    public async Task<PagedResult<OpportunitySummary>> Handle(
        GetStaleQuery request,
        CancellationToken cancellationToken)
    {
        return await queryRepo.ListStaleAsync(
            tenantContext.TenantId,
            tenantContext.BuId,
            request.Page,
            cancellationToken).ConfigureAwait(false);
    }
}

// =========================================================================
// ListSavedFiltersQuery
// =========================================================================

/// <summary>
/// Query para listar filtros salvos do usuário autenticado.
/// Mapeia: Req 19, design §5.2, TASK-12 ST-04.
/// </summary>
public sealed record ListSavedFiltersQuery : IRequest<IReadOnlyList<SavedFilters.SavedFilter>>, IQuery, IAuthenticatedCommand
{
    public UserRole UserRole => UserRole.Viewer;
    public string CorrelationId { get; init; } = string.Empty;
}

/// <summary>Handler de ListSavedFiltersQuery.</summary>
public sealed class ListSavedFiltersHandler(
    SavedFilters.ISavedFilterRepository filterRepo,
    TenantContext tenantContext)
    : IRequestHandler<ListSavedFiltersQuery, IReadOnlyList<SavedFilters.SavedFilter>>
{
    public async Task<IReadOnlyList<SavedFilters.SavedFilter>> Handle(
        ListSavedFiltersQuery request,
        CancellationToken cancellationToken)
    {
        return await filterRepo.ListByUserAsync(
            tenantContext.TenantId,
            tenantContext.ActorId,
            cancellationToken).ConfigureAwait(false);
    }
}
