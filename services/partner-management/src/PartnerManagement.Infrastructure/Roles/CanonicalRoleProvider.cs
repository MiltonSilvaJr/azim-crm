using Microsoft.Extensions.Caching.Memory;
using PartnerManagement.Application.Ports;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.ValueObjects;

namespace PartnerManagement.Infrastructure.Roles;

/// <summary>
/// Implementação de <see cref="ICanonicalRoleProvider"/> com cache por tenant.
/// Retorna o seed canônico padrão (Indicador, Revendedor, Distribuidor, Integrador) como default.
/// Cache de curta duração por tenant (TTL configurável) — a lista muda raramente (design §6.2, DD-005).
/// Mapeia: Req 5.2, Req 5.3, DD-005, design §6.2, TASK-20.
/// </summary>
public sealed class CanonicalRoleProvider : ICanonicalRoleProvider, ICanonicalRoleProviderPort
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    // Prefixo de chave de cache — inclui tenant_id para segregação (design §14)
    private const string CacheKeyPrefix = "canonical_roles_";

    /// <summary>
    /// Inicializa o provider com o cache de memória.
    /// </summary>
    /// <param name="cache">Cache de memória registrado no DI.</param>
    public CanonicalRoleProvider(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc/>
    public bool IsCanonical(string role, Guid tenantId)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        IReadOnlyList<string> canonicalRoles = GetCanonicalRoles(tenantId);

        // Comparação case-insensitive (Req 5.2)
        return canonicalRoles.Any(r => string.Equals(r, role.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Retorna a lista canônica de papéis para o tenant.
    /// Cache incluindo <paramref name="tenantId"/> na chave (design §14).
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    public IReadOnlyList<string> GetCanonicalRoles(Guid tenantId)
    {
        string cacheKey = $"{CacheKeyPrefix}{tenantId}";

        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;

            // No MVP: retorna o seed canônico padrão.
            // Evolução futura: buscar da tabela de configuração do tenant (DD-005).
            return (IReadOnlyList<string>)PartnerRole.DefaultCanonicalRoles;
        })!;
    }
}
