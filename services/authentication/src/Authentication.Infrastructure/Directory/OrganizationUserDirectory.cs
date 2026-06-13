using Authentication.Application.Ports;
using Authentication.Application.Ports.Results;
using Authentication.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Authentication.Infrastructure.Directory;

/// <summary>
/// Adapter de leitura que resolve a referência opaca do provedor de identidade
/// para <c>user_id</c> interno, papéis e memberships.
///
/// Consulta as tabelas <c>users</c> e <c>user_memberships</c> (módulo organization).
/// NUNCA retorna <c>identity_uid</c> (ProviderUserRef) ao chamador Application (DD-001).
/// As consultas são escopadas ao <c>tenant_id</c> corrente para isolar tenants.
///
/// DÍVIDA TÉCNICA (TASK-14):
/// As tabelas consultadas pertencem ao módulo organization, que ainda não existe.
/// O schema é criado localmente para testes via Testcontainers (PostgreSQL).
/// Em produção, este adapter lê de um banco compartilhado com o módulo dono.
/// Esta dependência deve ser formalizada quando o módulo organization for criado.
///
/// Mapeia: Req 5, design.md § 6.1, DD-001, TASK-14.
/// </summary>
public sealed class OrganizationUserDirectory : IUserDirectory
{
    private readonly DirectoryDbContext _context;

    public OrganizationUserDirectory(DirectoryDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task<UserDirectoryResult?> FindUserAsync(
        string providerUserRef,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerUserRef))
            return null;

        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.ProviderUserRef == providerUserRef
                        && u.TenantId == tenantId
                        && u.Status == "active")
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return null;

        var memberships = await LoadMembershipsAsync(user.Id, tenantId, cancellationToken);

        return new UserDirectoryResult
        {
            UserId = user.Id,
            Email = user.Email,
            Roles = [user.AuthMethod], // roles derivados do schema real em produção
            Memberships = memberships,
            IsActive = true,
            SignInProvider = user.AuthMethod,
        };
    }

    /// <inheritdoc/>
    public async Task<bool> IsEmailActiveAsync(
        string email,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        return await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == email.ToLowerInvariant()
                           && u.TenantId == tenantId
                           && u.Status == "active",
                      cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<UserDirectoryResult?> FindUserByEmailAsync(
        string email,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Email == email.ToLowerInvariant()
                        && u.TenantId == tenantId
                        && u.Status == "active")
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return null;

        var memberships = await LoadMembershipsAsync(user.Id, tenantId, cancellationToken);

        return new UserDirectoryResult
        {
            UserId = user.Id,
            Email = user.Email,
            Roles = [user.AuthMethod],
            Memberships = memberships,
            IsActive = true,
            SignInProvider = user.AuthMethod,
        };
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Carrega os memberships do usuário no tenant.
    /// A consulta é escopada ao tenant_id para garantir isolamento (DD-007).
    ///
    /// NOTA: Em produção, o SET app.current_tenant + RLS (DEC-006) garantem
    /// isolamento adicional na camada de banco de dados.
    /// </summary>
    private async Task<MembershipSet> LoadMembershipsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var entries = await _context.UserMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.TenantId == tenantId)
            .Select(m => new MembershipEntry(m.BuId, m.Role))
            .ToListAsync(cancellationToken);

        return MembershipSet.Create(entries, DateTimeOffset.UtcNow);
    }
}
