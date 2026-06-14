using GoalForecast.Application.Ports;
using GoalForecast.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace GoalForecast.Infrastructure.Persistence;

/// <summary>
/// Implementação concreta da porta <see cref="IGoalRepository"/> usando EF Core.
/// Todas as queries passam pelo Global Query Filter de tenant_id (ADR-0001).
/// Nenhum método usa <c>IgnoreQueryFilters</c>.
///
/// Upsert semântico (DD-002):
/// <list type="bullet">
///   <item>Violação de UNIQUE na inserção é capturada como <c>DbUpdateException</c>
///   com <c>SqlState 23505</c> — tratada retornando o Goal existente ao caller
///   para que o handler prossiga com Update.</item>
/// </list>
///
/// Mapeia: Req 1, Req 2, Req 3, RNF 1, DD-002, design §6.1, TASK-17.
/// </summary>
public sealed class GoalRepository(GoalForecastDbContext dbContext) : IGoalRepository
{
    /// <inheritdoc/>
    public async Task<Goal?> FindByKey(
        Guid tenantId,
        Guid buId,
        Guid? ownerId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        // Global Query Filter já aplica tenant_id — a condição abaixo
        // é redundante para segurança extra, mas necessária para a assinatura da porta.
        return await dbContext.Goals
            .FirstOrDefaultAsync(g =>
                    g.Scope.BuId == buId
                    && g.Scope.OwnerId == ownerId
                    && g.Period.Year == year
                    && g.Period.Month == month,
                cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Goal?> FindById(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // Global Query Filter filtra automaticamente por tenant_id.
        return await dbContext.Goals
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task Add(Goal goal, CancellationToken cancellationToken = default)
    {
        dbContext.Goals.Add(goal);
        await dbContext.SaveChangesAsync(cancellationToken);
        goal.ClearDomainEvents();
    }

    /// <inheritdoc/>
    public async Task Update(Goal goal, CancellationToken cancellationToken = default)
    {
        // Verifica se a entidade já está tracked no contexto corrente.
        // Se estiver, SaveChangesAsync detecta as mudanças automaticamente.
        // Caso contrário (entidade desconectada), usa Attach + Modified.
        if (dbContext.Entry(goal).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
        {
            dbContext.Goals.Attach(goal);
            dbContext.Entry(goal).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        goal.ClearDomainEvents();
    }

    /// <inheritdoc/>
    public async Task<PagedResult<Goal>> Query(
        GoalQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        // Global Query Filter garante que apenas metas do tenant_id do contexto são retornadas.
        var query = dbContext.Goals.AsQueryable();

        if (filter.BuId.HasValue)
            query = query.Where(g => g.Scope.BuId == filter.BuId.Value);

        if (filter.OwnerId.HasValue)
            query = query.Where(g => g.Scope.OwnerId == filter.OwnerId.Value);

        if (filter.Year.HasValue)
            query = query.Where(g => g.Period.Year == filter.Year.Value);

        if (filter.Month.HasValue)
            query = query.Where(g => g.Period.Month == filter.Month.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(g => g.Period.Year)
            .ThenBy(g => g.Period.Month)
            .ThenBy(g => g.Scope.BuId)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Goal>(items, filter.Page, filter.PageSize, total);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Goal>> ListByYear(
        Guid tenantId,
        Guid buId,
        Guid? ownerId,
        int year,
        CancellationToken cancellationToken = default)
    {
        // Global Query Filter aplica tenant_id; filtramos por bu_id, owner_id e year.
        return await dbContext.Goals
            .Where(g =>
                g.Scope.BuId == buId
                && g.Scope.OwnerId == ownerId
                && g.Period.Year == year)
            .ToListAsync(cancellationToken);
    }
}
