using AuditLog.Domain.Aggregates;
using AuditLog.Domain.Repositories;
using AuditLog.Domain.ValueObjects;
using AuditLog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuditLog.Infrastructure.Repositories;

/// <summary>
/// Implementação de <see cref="IAuditLogRepository"/> sobre EF Core + PostgreSQL.
/// Expõe apenas <see cref="AddAsync"/> e operações de leitura.
/// Operações de UPDATE/DELETE são intencionalmente ausentes (append-only, RNF-001).
/// </summary>
public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AuditLogDbContext _dbContext;

    /// <summary>Inicializa o repositório com o DbContext corrente.</summary>
    /// <param name="dbContext">DbContext com filtro global de tenant aplicado.</param>
    public AuditLogRepository(AuditLogDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Apenas registra o aggregate no ChangeTracker do DbContext.
    /// O commit real ocorre quando <c>SaveChangesAsync</c> é chamado pela unidade de trabalho
    /// do módulo de origem (DD-001 — transação única compartilhada com a escrita de negócio).
    /// </remarks>
    public Task AddAsync(AuditLogAggregate auditLog, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        _dbContext.AuditLogs.Add(auditLog);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditLogAggregate>> FindByEntityAsync(
        TenantId tenantId,
        EntityReference entityReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(entityReference);

        // O filtro global de tenant_id já está aplicado no DbContext (DD-003).
        // EntityReference é mapeada via backing fields EntityTypePersisted e EntityIdPersisted.
        var entityType = entityReference.EntityType;
        var entityId = entityReference.EntityId;
        var results = await _dbContext.AuditLogs
            .Where(x =>
                x.EntityTypePersisted == entityType &&
                x.EntityIdPersisted == entityId)
            .OrderByDescending(x => x.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return results.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<AuditLogAggregate> Items, int TotalCount)> ListAsync(
        TenantId tenantId,
        string? entityType = null,
        Guid? entityId = null,
        Guid? actorId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantId);

        // O filtro global de tenant_id já está aplicado no DbContext (DD-003).
        var query = _dbContext.AuditLogs.AsQueryable();

        // EntityReference é mapeada via backing fields EntityTypePersisted e EntityIdPersisted.
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(x => x.EntityTypePersisted == entityType);

        if (entityId.HasValue)
        {
            var eid = entityId.Value;
            query = query.Where(x => x.EntityIdPersisted == eid);
        }

        if (actorId.HasValue)
        {
            var aid = actorId.Value;
            query = query.Where(x => x.ActorId.Value == aid);
        }

        if (from.HasValue)
            query = query.Where(x => x.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.CreatedAt <= to.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return (items.AsReadOnly(), totalCount);
    }
}
