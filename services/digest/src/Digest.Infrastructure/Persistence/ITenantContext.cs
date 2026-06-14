namespace Digest.Infrastructure.Persistence;

/// <summary>
/// Contexto de tenant para o escopo de processamento atual.
/// Usado pelo Global Query Filter do <see cref="DigestDbContext"/> e pelo
/// <see cref="Security.TenantConnectionInterceptor"/> para <c>SET app.current_tenant</c>.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Identificador do tenant em processamento.
    /// Pode ser <see cref="Guid.Empty"/> antes de entrar no escopo de um tenant
    /// (ex.: durante <c>SelectEligibleTenantsQuery</c> — design §5.2).
    /// </summary>
    Guid CurrentTenantId { get; }
}
