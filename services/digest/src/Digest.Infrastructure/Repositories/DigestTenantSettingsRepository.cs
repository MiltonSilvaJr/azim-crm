using Digest.Application.Repositories;
using Digest.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Digest.Infrastructure.Repositories;

/// <summary>
/// Implementação de <see cref="IDigestTenantSettingsRepository"/> usando EF Core 9 e PostgreSQL.
/// Consulta <c>digest_tenant_settings</c> protegida por Global Query Filter e RLS (ADR-0001, VAL-ACT-02).
/// </summary>
public sealed class DigestTenantSettingsRepository : IDigestTenantSettingsRepository
{
    private readonly DigestDbContext _db;

    /// <summary>
    /// Constrói o repositório com o DbContext injetado.
    /// </summary>
    public DigestTenantSettingsRepository(DigestDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<int?> GetActionTokenTtlHoursAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenant_id não pode ser vazio.", nameof(tenantId));

        // Global Query Filter garante escopo por tenant.
        // AsNoTracking: operação de leitura pura, sem necessidade de rastreamento.
        var settings = await _db.DigestTenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        return settings?.ActionTokenTtlHours;
    }
}
