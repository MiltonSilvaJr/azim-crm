using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Ports.Results;
using Authentication.Domain.Specifications;
using Authentication.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;

namespace Authentication.Application.Services;

/// <summary>
/// Serviço de aplicação que compõe o <see cref="AuthContext"/> a partir do resultado
/// de verificação do token.
///
/// Fluxo:
///   1. Verifica cache (chave <c>auth:membership:{tenant_id}:{providerUserRef}</c>).
///   2. Se cache miss, chama <see cref="IUserDirectory.FindUserAsync"/>.
///   3. Aplica <see cref="ActiveUserSpec"/>: sem <c>user_id</c> ativo → AUTH-ERR-005 (403).
///   4. Produz <see cref="AuthContext"/> imutável sem <c>identity_uid</c> (DD-001).
///
/// Cache isola por <c>tenant_id</c>: nenhuma entrada é compartilhada entre tenants
/// (design.md § 6.2, § 14).
///
/// Mapeia: TASK-06, design.md § 5.3, Req 5, Req 5.4, RNF 2.3.
/// </summary>
public sealed class AuthContextComposer
{
    private readonly IUserDirectory _userDirectory;
    private readonly IMemoryCache _cache;

    // TTL padrão do cache de membership (~5 min, configurável — design.md § 6.2, DD-007)
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Inicializa o compositor com o diretório de usuários e o cache.
    /// </summary>
    /// <param name="userDirectory">Porta de saída para o módulo organization.</param>
    /// <param name="cache">Cache em memória para memberships (RNF 2.3).</param>
    public AuthContextComposer(IUserDirectory userDirectory, IMemoryCache cache)
    {
        _userDirectory = userDirectory;
        _cache = cache;
    }

    /// <summary>
    /// Compõe o <see cref="AuthContext"/> a partir do resultado de verificação do token.
    ///
    /// Aplica <see cref="ActiveUserSpec"/>: usuário sem <c>user_id</c> ativo →
    /// <see cref="IdentityProviderException"/> AUTH-ERR-005 (403).
    ///
    /// O <see cref="AuthContext"/> resultante nunca contém o <c>ProviderUserRef</c>
    /// nem qualquer identificador do IdP (DD-001).
    /// </summary>
    /// <param name="verifyResult">Resultado da verificação do token pelo IdP.</param>
    /// <param name="tenantId">UUID do tenant resolvido pelo slug.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Contexto de autenticação imutável escopado ao tenant.</returns>
    /// <exception cref="IdentityProviderException">
    /// Lançada com código AUTH-ERR-005 quando o usuário não está ativo no tenant (Req 5.4).
    /// </exception>
    public async Task<AuthContext> ComposeAsync(
        VerifyTokenResult verifyResult,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var userResult = await ResolveUserWithCacheAsync(
            verifyResult.ProviderUserRef,
            tenantId,
            cancellationToken);

        // Aplica ActiveUserSpec: identity_uid válido deve ter user_id ativo no tenant (Req 5.4)
        if (userResult is null || !ActiveUserSpec.IsSatisfiedBy(userResult.UserId, userResult.IsActive))
        {
            throw new IdentityProviderException(
                "AUTH-ERR-005",
                "Usuário sem user_id ativo no tenant (Req 5.4).");
        }

        return AuthContext.Create(
            userId: userResult.UserId,
            tenantId: tenantId,
            email: userResult.Email,
            roles: userResult.Roles,
            memberships: userResult.Memberships);
    }

    // Cache read-through: chave isola por tenant_id (design.md § 6.2, § 14)
    private async Task<UserDirectoryResult?> ResolveUserWithCacheAsync(
        string providerUserRef,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(tenantId, providerUserRef);

        if (_cache.TryGetValue(cacheKey, out UserDirectoryResult? cached))
            return cached;

        var result = await _userDirectory.FindUserAsync(providerUserRef, tenantId, cancellationToken);

        if (result is not null)
        {
            _cache.Set(cacheKey, result, CacheTtl);
        }

        return result;
    }

    /// <summary>
    /// Constrói a chave de cache isolada por tenant.
    /// Padrão: <c>auth:membership:{tenant_id}:{providerUserRef}</c>
    /// (design.md § 6.2, § 14 — nenhuma entrada compartilhada entre tenants).
    /// </summary>
    private static string BuildCacheKey(Guid tenantId, string providerUserRef) =>
        $"auth:membership:{tenantId}:{providerUserRef}";
}
