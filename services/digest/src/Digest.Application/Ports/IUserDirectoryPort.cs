using Digest.Application.Models;

namespace Digest.Application.Ports;

/// <summary>
/// Porta de leitura somente-leitura do diretório de usuários e tenants.
/// Fonte: módulo organization (design §6.4, Req 3, Req 2).
/// Implementação concreta vive em <c>Digest.Infrastructure</c>.
/// </summary>
/// <remarks>
/// Restrições:
/// <list type="bullet">
///   <item>
///     <see cref="GetActiveTenantInfosAsync"/> é cross-tenant por natureza administrativa
///     (roda antes de setar <c>app.current_tenant</c> — design §5.2).
///   </item>
///   <item>Demais métodos são restritos ao <c>tenant_id</c> (Req 6.4).</item>
///   <item>Retorna lista vazia quando não há dados — nunca <see langword="null"/> em listas.</item>
/// </list>
/// </remarks>
public interface IUserDirectoryPort
{
    /// <summary>
    /// Retorna as informações de todos os tenants ativos para seleção de elegibilidade.
    /// Executado antes do escopo de tenant (design §5.2, Req 2).
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de tenants ativos; nunca <see langword="null"/>.</returns>
    Task<IReadOnlyList<TenantInfo>> GetActiveTenantInfosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna os usuários ativos no tenant especificado com seus papéis.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Obrigatório.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de usuários ativos; nunca <see langword="null"/>.</returns>
    Task<IReadOnlyList<UserInfo>> GetActiveUsersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
