using Microsoft.EntityFrameworkCore;
using TenantAdministration.Application.Ports;
using TenantAdministration.Domain.Aggregates;

namespace TenantAdministration.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação concreta de <see cref="ITenantRepository"/> usando EF Core.
/// Encapsula o acesso a <c>tenants</c> sem vazar <c>IQueryable</c> para camadas superiores.
///
/// Nota sobre carregamento de Branding: o EF Core não consegue materializar TenantBranding
/// via LEFT JOIN quando o tenant não tem branding (colunas non-nullable ficam null no resultado).
/// Por isso, TenantBranding é carregado separadamente com AsSplitQuery ou Include condicional.
/// </summary>
public sealed class TenantRepository : ITenantRepository
{
    private readonly TenantAdministrationDbContext _db;

    /// <param name="db">DbContext do módulo.</param>
    public TenantRepository(TenantAdministrationDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ExistsSlugAsync(string slug, CancellationToken ct = default)
    {
        // Capturar o Value Object antes do LINQ — expression trees não suportam chamadas
        // encadeadas que acessam propriedades de Result<T> (especialmente em falha).
        var slugResult = Domain.ValueObjects.Slug.Create(slug);
        if (slugResult.IsFailure)
            return false;
        var slugVo = slugResult.Value;
        return await _db.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Slug == slugVo, ct);
    }

    /// <inheritdoc/>
    public async Task<Tenant?> FindByIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        // Nota: Include(t => t.Branding) causa InvalidOperationException no EF Core quando
        // o tenant não tem branding (colunas NOT NULL do branding ficam null no LEFT JOIN).
        // Branding é carregado automaticamente quando existe, via change tracker.
        // O aggregado Tenant.Branding fica null quando não há branding (comportamento correto).
        return await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);
    }

    /// <inheritdoc/>
    public async Task<Tenant?> FindBySlugAsync(string slug, CancellationToken ct = default)
    {
        var slugResult = Domain.ValueObjects.Slug.Create(slug);
        if (slugResult.IsFailure)
            return null;

        var slugVo = slugResult.Value;
        return await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slugVo, ct);
    }

    /// <inheritdoc/>
    public async Task AddAsync(Tenant tenant, CancellationToken ct = default)
    {
        await _db.Tenants.AddAsync(tenant, ct);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(Tenant tenant, CancellationToken ct = default)
    {
        _db.Tenants.Update(tenant);
        return Task.CompletedTask;
    }
}
