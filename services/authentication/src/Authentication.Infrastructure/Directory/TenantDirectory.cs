using Authentication.Application.Ports;
using Authentication.Application.Ports.Results;
using Microsoft.EntityFrameworkCore;

namespace Authentication.Infrastructure.Directory;

/// <summary>
/// Adapter de leitura que resolve <c>slug → tenant_id</c> e <c>identity_tenant_id</c>
/// consultando a tabela <c>tenants</c> (módulo tenant-administration).
///
/// A resolução é cross-tenant por natureza (executa em conexão de catálogo,
/// antes de qualquer <c>SET app.current_tenant</c>). Somente leitura.
///
/// DÍVIDA TÉCNICA (TASK-14):
/// As tabelas consultadas pertencem ao módulo tenant-administration, que ainda não
/// existe. O schema é criado localmente para testes via Testcontainers (PostgreSQL).
/// Em produção, este adapter lê de um banco compartilhado com o módulo dono.
/// Esta dependência deve ser formalizada quando o módulo tenant-administration for criado.
///
/// Mapeia: Req 1, design.md § 6.1, § 6.7, DD-001, TASK-14.
/// </summary>
public sealed class TenantDirectory : ITenantDirectory
{
    private readonly DirectoryDbContext _context;

    public TenantDirectory(DirectoryDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task<TenantResolutionResult?> ResolveSlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        // Normalização do slug (lowercase, trim) para busca consistente
        var normalizedSlug = slug.Trim().ToLowerInvariant();

        var tenant = await _context.Tenants
            .AsNoTracking()
            .Where(t => t.Slug == normalizedSlug
                        && t.Status == "active")
            .Select(t => new { t.Id, t.IdentityTenantId })
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null)
            return null;

        return new TenantResolutionResult
        {
            TenantId = tenant.Id,
            IdentityTenantId = tenant.IdentityTenantId,
        };
    }
}
