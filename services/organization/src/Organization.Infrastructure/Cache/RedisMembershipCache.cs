using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Organization.Application.Ports;
using StackExchange.Redis;

namespace Organization.Infrastructure.Cache;

/// <summary>
/// Implementação de <see cref="IMembershipCache"/> via Redis (StackExchange.Redis).
/// Chave: <c>org:rbac:{tenant_id}:{user_id}</c>.
/// Sem PII — apenas identificadores Guid e papéis canônicos (Req 13.3).
/// Degradação segura: exceções Redis são silenciadas; falha retorna null ou é ignorada (RNF 5.3).
/// </summary>
public sealed class RedisMembershipCache : IMembershipCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisMembershipCache> _logger;
    private readonly TimeSpan _ttl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>Inicializa o adapter com a conexão Redis e opções de TTL.</summary>
    public RedisMembershipCache(
        IConnectionMultiplexer redis,
        IOptions<MembershipCacheOptions> options,
        ILogger<RedisMembershipCache> logger)
    {
        _redis = redis;
        _logger = logger;
        _ttl = options.Value.Ttl;
    }

    /// <inheritdoc/>
    public async Task<MembershipCacheEntry?> GetAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(tenantId, userId);
            var value = await db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<MembershipCacheEntry>((string)value!, JsonOptions);
        }
        catch (Exception ex)
        {
            // Degradação segura: Redis indisponível não propaga exceção (RNF 5.3).
            // Log sem PII: apenas tenant_id e a exceção técnica.
            _logger.LogWarning(ex,
                "Cache miss degradado: falha ao ler memberships do Redis para tenant {TenantId}.",
                tenantId);

            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync(
        Guid tenantId,
        Guid userId,
        MembershipCacheEntry entry,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(tenantId, userId);
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            await db.StringSetAsync(key, json, _ttl);
        }
        catch (Exception ex)
        {
            // Falha silenciosa: escrita no cache não é crítica (RNF 5.3).
            _logger.LogWarning(ex,
                "Cache write degradado: falha ao gravar memberships no Redis para tenant {TenantId}.",
                tenantId);
        }
    }

    /// <inheritdoc/>
    public async Task InvalidateAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(tenantId, userId);
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            // Falha de invalidação: degrada para expiração por TTL (RNF 5.3).
            _logger.LogWarning(ex,
                "Cache invalidation degradado: falha ao invalidar memberships no Redis para tenant {TenantId}.",
                tenantId);
        }
    }

    // Chave sem PII: apenas GUIDs de tenant e usuário.
    private static RedisKey BuildKey(Guid tenantId, Guid userId)
        => $"org:rbac:{tenantId:D}:{userId:D}";
}
