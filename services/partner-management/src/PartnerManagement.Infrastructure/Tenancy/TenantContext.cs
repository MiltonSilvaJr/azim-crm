using PartnerManagement.Application.Ports;

namespace PartnerManagement.Infrastructure.Tenancy;

/// <summary>
/// Implementação mutável de <see cref="ITenantContext"/> para uso em runtime.
/// Resolvida e populada pelo <c>TenantScopeBehavior</c> a partir do token JWT.
/// Registrada com escopo de vida <c>Scoped</c> no DI (um por requisição HTTP).
/// Mapeia: RNF 1, DD-001, design §5.4, TASK-17.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private Guid _currentTenantId = Guid.Empty;

    /// <inheritdoc/>
    public Guid CurrentTenantId => _currentTenantId;

    /// <summary>
    /// Define o tenant corrente da requisição.
    /// Chamado pelo <c>TenantScopeBehavior</c> antes de qualquer handler.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant autenticado.</param>
    /// <exception cref="ArgumentException">Lançada quando <paramref name="tenantId"/> é <see cref="Guid.Empty"/>.</exception>
    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId não pode ser Guid.Empty.", nameof(tenantId));
        }

        _currentTenantId = tenantId;
    }
}
