namespace Digest.Infrastructure.Persistence;

/// <summary>
/// Implementação mutável do <see cref="ITenantContext"/> com escopo de requisição/comando.
/// Registrado como <c>Scoped</c> no DI; o tenant é setado pelo
/// <c>TenantScopeBehavior</c> no início de cada command (design §5.4).
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private Guid _currentTenantId = Guid.Empty;

    /// <inheritdoc/>
    public Guid CurrentTenantId => _currentTenantId;

    /// <summary>
    /// Define o tenant atual para o escopo de processamento.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Não pode ser vazio.</param>
    /// <exception cref="ArgumentException">Quando <paramref name="tenantId"/> é vazio.</exception>
    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenant_id não pode ser vazio ao setar o contexto.", nameof(tenantId));
        _currentTenantId = tenantId;
    }

    /// <summary>Limpa o contexto de tenant (fim do escopo).</summary>
    public void Clear() => _currentTenantId = Guid.Empty;
}
