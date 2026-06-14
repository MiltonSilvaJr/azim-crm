namespace Organization.Application.Ports;

/// <summary>
/// Port de saída para o cache de memberships de RBAC no Redis.
/// Chave: <c>org:rbac:{tenant_id}:{user_id}</c>; serialização sem PII (Req 13.3).
/// Degradação segura: indisponibilidade do Redis não lança exceção para o handler (RNF 5.3).
/// </summary>
public interface IMembershipCache
{
    /// <summary>
    /// Recupera o contexto de RBAC do usuário do cache.
    /// Retorna <c>null</c> em cache miss ou quando Redis está indisponível.
    /// </summary>
    Task<MembershipCacheEntry?> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Armazena o contexto de RBAC do usuário no cache com TTL configurável.
    /// Silencia erros de Redis — falha de escrita não propaga exceção.
    /// </summary>
    Task SetAsync(Guid tenantId, Guid userId, MembershipCacheEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida o contexto de RBAC do usuário no cache (DEL).
    /// Chamado após toda alteração de membership.
    /// Silencia erros de Redis — falha de invalidação degrada para TTL curto.
    /// </summary>
    Task InvalidateAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Entrada do cache de memberships. Sem PII (apenas identificadores e papéis).
/// </summary>
/// <param name="RolesByBu">Mapa de BU → papel para o usuário.</param>
/// <param name="CachedAt">Instante de criação da entrada no cache.</param>
public sealed record MembershipCacheEntry(
    IReadOnlyDictionary<Guid, string> RolesByBu,
    DateTimeOffset CachedAt);
