using Microsoft.EntityFrameworkCore;
using OpportunityPipeline.Application.Opportunities.Queries;
using OpportunityPipeline.Domain.Opportunities;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação do IOpportunityQueryRepository via EF Core.
/// CQRS read-side: projeta DTOs diretamente sem carregar o agregado completo.
/// Global Query Filter por tenant_id já aplicado pelo DbContext.
/// Mapeia: design §5.2, TASK-13.
/// </summary>
public sealed class OpportunityQueryRepository(OpportunityDbContext context)
    : IOpportunityQueryRepository
{
    /// <inheritdoc/>
    public async Task<Opportunity?> GetByIdWithDetailsAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await context.Opportunities
            .AsNoTracking()
            .Include(o => o.Transitions)
            .Include(o => o.Commissions)
            .Include(o => o.Contacts)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<OpportunitySummary>> ListAsync(
        Guid tenantId,
        Guid buId,
        OpportunityFilter filter,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var query = context.Opportunities
            .AsNoTracking()
            .Where(o => o.BuId == buId);

        // Aplicar filtros opcionais
        if (filter.OwnerId.HasValue)
            query = query.Where(o => o.OwnerId == filter.OwnerId.Value);

        if (filter.PartnerId.HasValue)
            query = query.Where(o => o.PartnerId == filter.PartnerId.Value);

        if (filter.StageCategory.HasValue)
            query = query.Where(o => o.StageCategory == filter.StageCategory.Value);

        if (filter.IsStale.HasValue)
            query = query.Where(o => o.IsStale == filter.IsStale.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
            query = query.Where(o => EF.Functions.ILike(o.Title, $"%{filter.SearchText}%"));

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(o => o.UpdatedAt)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .Select(o => new OpportunitySummary(
                o.Id,
                o.Number.Value,
                o.Title,
                o.OwnerId,
                o.AccountId,
                o.PartnerId,
                o.Stage.Name,
                o.StageCategory,
                o.ContractValue.Setup.AmountInCents + o.ContractValue.Mensal.AmountInCents * o.ContractValue.DuracaoMeses,
                (o.ContractValue.Setup.AmountInCents + o.ContractValue.Mensal.AmountInCents * o.ContractValue.DuracaoMeses) * o.Probability.Value / 100,
                o.IsStale,
                o.ExpectedCloseDate.HasValue && o.ExpectedCloseDate < DateOnly.FromDateTime(DateTime.UtcNow) && o.StageCategory == StageCategory.Open,
                o.CreatedAt,
                o.UpdatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<OpportunitySummary>(items, totalCount, page.Page, page.PageSize);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<KanbanColumn>> GetKanbanAsync(
        Guid tenantId,
        Guid buId,
        int stagePageSize,
        CancellationToken cancellationToken = default)
    {
        // Agrupa por estágio com somas SQL (RNF 1.3)
        var stageSummaries = await context.Opportunities
            .AsNoTracking()
            .Where(o => o.BuId == buId && o.StageCategory == StageCategory.Open)
            .GroupBy(o => new { o.Stage.StageId, o.Stage.Name, o.Stage.Order })
            .Select(g => new
            {
                g.Key.StageId,
                g.Key.Name,
                g.Key.Order,
                TotalValueCents = g.Sum(o => o.ContractValue.Setup.AmountInCents + o.ContractValue.Mensal.AmountInCents * o.ContractValue.DuracaoMeses),
                ForecastPonderadoCents = g.Sum(o => (o.ContractValue.Setup.AmountInCents + o.ContractValue.Mensal.AmountInCents * o.ContractValue.DuracaoMeses) * o.Probability.Value / 100),
                TotalCount = g.Count()
            })
            .OrderBy(s => s.Order)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Carrega cards do primeiro estágio paginado
        var columns = new List<KanbanColumn>();
        foreach (var stage in stageSummaries)
        {
            var cards = await context.Opportunities
                .AsNoTracking()
                .Where(o => o.BuId == buId && o.Stage.StageId == stage.StageId)
                .OrderByDescending(o => o.UpdatedAt)
                .Take(stagePageSize)
                .Select(o => new OpportunitySummary(
                    o.Id,
                    o.Number.Value,
                    o.Title,
                    o.OwnerId,
                    o.AccountId,
                    o.PartnerId,
                    o.Stage.Name,
                    o.StageCategory,
                    o.ContractValue.Setup.AmountInCents + o.ContractValue.Mensal.AmountInCents * o.ContractValue.DuracaoMeses,
                    (o.ContractValue.Setup.AmountInCents + o.ContractValue.Mensal.AmountInCents * o.ContractValue.DuracaoMeses) * o.Probability.Value / 100,
                    o.IsStale,
                    false,
                    o.CreatedAt,
                    o.UpdatedAt))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            columns.Add(new KanbanColumn(
                stage.StageId,
                stage.Name,
                stage.Order,
                stage.TotalValueCents,
                stage.ForecastPonderadoCents,
                stage.TotalCount,
                cards));
        }

        return columns.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TimelineEntry>> GetTimelineAsync(
        Guid tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken = default)
    {
        return await context.StageTransitions
            .AsNoTracking()
            .Where(t => t.OpportunityId == opportunityId)
            .OrderBy(t => t.OccurredAt)
            .Select(t => new TimelineEntry(
                t.Id,
                t.FromStageId.HasValue ? t.FromStageId.ToString() : null,
                t.ToStageId.ToString(),
                t.FromCategory.HasValue ? t.FromCategory.Value.ToString() : "none",
                t.ToCategory.ToString(),
                t.OccurredAt,
                t.ActorId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CommissionDetail>> GetCommissionsAsync(
        Guid tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken = default)
    {
        return await context.PartnerCommissions
            .AsNoTracking()
            .Where(c => c.OpportunityId == opportunityId)
            .Select(c => new CommissionDetail(
                c.PartnerId,
                c.Terms.Role.ToString(),
                c.Terms.PctSetup,
                c.Terms.PctRecorrente,
                c.Terms.ValorFixo != null ? c.Terms.ValorFixo.AmountInCents : 0L,
                c.Terms.MesesComissionados,
                c.Calculation.ComissaoTotal.AmountInCents,
                0L, // forecast_liquido calculado na query completa
                c.IsSnapshot,
                c.SnapshotAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Guid>> ListOpenOpportunityIdsAsync(
        Guid tenantId,
        Guid buId,
        CancellationToken cancellationToken = default)
    {
        return await context.Opportunities
            .AsNoTracking()
            .Where(o => o.BuId == buId && o.StageCategory == StageCategory.Open)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<OpportunitySummary>> ListStaleAsync(
        Guid tenantId,
        Guid buId,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var query = context.Opportunities
            .AsNoTracking()
            .Where(o => o.BuId == buId && o.IsStale && o.StageCategory == StageCategory.Open);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(o => o.UpdatedAt)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .Select(o => new OpportunitySummary(
                o.Id,
                o.Number.Value,
                o.Title,
                o.OwnerId,
                o.AccountId,
                o.PartnerId,
                o.Stage.Name,
                o.StageCategory,
                o.ContractValue.Setup.AmountInCents + o.ContractValue.Mensal.AmountInCents * o.ContractValue.DuracaoMeses,
                (o.ContractValue.Setup.AmountInCents + o.ContractValue.Mensal.AmountInCents * o.ContractValue.DuracaoMeses) * o.Probability.Value / 100,
                true,
                false,
                o.CreatedAt,
                o.UpdatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<OpportunitySummary>(items, totalCount, page.Page, page.PageSize);
    }

    /// <inheritdoc/>
    public async Task<(long TotalValueCents, long ForecastPonderadoCents, long ForecastLiquidoCents)> GetForecastAsync(
        Guid tenantId,
        Guid buId,
        CancellationToken cancellationToken = default)
    {
        var result = await context.Opportunities
            .AsNoTracking()
            .Where(o => o.BuId == buId && o.StageCategory == StageCategory.Open)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalValue = g.Sum(o => o.ContractValue.Setup.AmountInCents + o.ContractValue.Mensal.AmountInCents * o.ContractValue.DuracaoMeses),
                ForecastPonderado = g.Sum(o => (o.ContractValue.Setup.AmountInCents + o.ContractValue.Mensal.AmountInCents * o.ContractValue.DuracaoMeses) * o.Probability.Value / 100)
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return result is null
            ? (0L, 0L, 0L)
            : (result.TotalValue, result.ForecastPonderado, result.ForecastPonderado); // forecast_liquido requer comissão
    }
}
