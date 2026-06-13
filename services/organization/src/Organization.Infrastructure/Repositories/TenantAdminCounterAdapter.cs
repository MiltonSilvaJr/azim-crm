using Microsoft.EntityFrameworkCore;
using Organization.Application.Ports;
using Organization.Domain.ValueObjects;
using Organization.Infrastructure.Persistence;

namespace Organization.Infrastructure.Repositories;

/// <summary>
/// Implementação concreta de <see cref="ITenantAdminCounter"/> via EF Core.
/// Conta TAdmins ativos dentro de uma transação para garantir atomicidade (DD-003).
/// </summary>
public sealed class TenantAdminCounterAdapter : ITenantAdminCounter
{
    private readonly OrganizationDbContext _context;

    /// <summary>Inicializa com o contexto de banco.</summary>
    public TenantAdminCounterAdapter(OrganizationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<int> CountActiveTenantAdminsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Conta usuários ativos com papel TAdmin no tenant corrente.
        // Deve ser executado dentro de uma transação para garantir consistência (DD-003).
        // O global query filter já aplica o filtro de tenant_id.
        // Comparação via m.Role == Role.TAdmin usa o value converter (Role -> string)
        // e é traduzida corretamente para role = 'TAdmin' em SQL.
        return await _context.Users
            .CountAsync(
                u => u.Active && u.Memberships.Any(m => m.Role == Role.TAdmin),
                cancellationToken);
    }
}
