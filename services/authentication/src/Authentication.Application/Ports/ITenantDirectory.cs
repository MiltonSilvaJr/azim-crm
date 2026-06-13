using Authentication.Application.Ports.Results;

namespace Authentication.Application.Ports;

/// <summary>
/// Porta de saída que abstrai a resolução de tenant a partir de um slug.
///
/// O <c>TenantResolutionMiddleware</c> usa esta porta para resolver o slug da URL
/// ou do header <c>X-Tenant-Slug</c> no <c>tenant_id</c> interno e no
/// <c>identity_tenant_id</c> correspondente no IdP.
///
/// A resolução é cross-tenant por natureza (executa em conexão de catálogo,
/// antes de qualquer <c>SET app.current_tenant</c>).
///
/// Mapeia: Req 1, design.md § 6.1, § 6.7, DD-001.
/// </summary>
public interface ITenantDirectory
{
    /// <summary>
    /// Resolve um slug de tenant para o <c>tenant_id</c> interno e o <c>identity_tenant_id</c>.
    ///
    /// Retorna <see langword="null"/> quando o slug não existe ou o tenant está inativo.
    /// </summary>
    /// <param name="slug">Slug normalizado do tenant (ex.: "acme").</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// Resultado com <c>tenant_id</c> e <c>identity_tenant_id</c>,
    /// ou <see langword="null"/> quando não encontrado/inativo.
    /// </returns>
    Task<TenantResolutionResult?> ResolveSlugAsync(
        string slug,
        CancellationToken cancellationToken = default);
}
