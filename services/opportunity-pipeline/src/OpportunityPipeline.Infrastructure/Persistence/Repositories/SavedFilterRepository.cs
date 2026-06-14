using Microsoft.EntityFrameworkCore;
using OpportunityPipeline.Application.SavedFilters;

namespace OpportunityPipeline.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repositório de filtros salvos do usuário.
/// Mapeia: Req 19, design §5.3, TASK-11 (implementação Infrastructure).
/// </summary>
public sealed class SavedFilterRepository(OpportunityDbContext context) : ISavedFilterRepository
{
    public async Task<bool> ExistsByNameAsync(
        Guid tenantId,
        Guid userId,
        string name,
        CancellationToken cancellationToken = default)
    {
        return await context.SavedFilters
            .AsNoTracking()
            .AnyAsync(f => f.TenantId == tenantId && f.UserId == userId && f.Name == name, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(
        SavedFilter filter,
        CancellationToken cancellationToken = default)
    {
        var entity = new SavedFilterEntity
        {
            Id = filter.Id,
            TenantId = filter.TenantId,
            UserId = filter.UserId,
            Name = filter.Name,
            CriteriaJson = filter.CriteriaJson,
            CreatedAt = filter.CreatedAt
        };

        context.SavedFilters.Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SavedFilter>> ListByUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entities = await context.SavedFilters
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(e => new SavedFilter(
            e.Id, e.TenantId, e.UserId, e.Name, e.CriteriaJson, e.CreatedAt)).ToList();
    }
}
