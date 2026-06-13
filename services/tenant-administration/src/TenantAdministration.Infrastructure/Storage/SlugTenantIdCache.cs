using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace TenantAdministration.Infrastructure.Storage;

/// <summary>
/// Cache de resolução <c>slug → tenantId</c> em Memorystore (Redis) com TTL curto.
/// Reduz carga no middleware de resolução de tenant (design.md §6.2, DD-006).
/// Invalidado em provisionamento de novo tenant.
/// </summary>
public sealed class SlugTenantIdCache
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<SlugTenantIdCache> _logger;

    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    /// <param name="cache">Cache distribuído (Redis via IDistributedCache).</param>
    /// <param name="logger">Logger estruturado.</param>
    public SlugTenantIdCache(IDistributedCache cache, ILogger<SlugTenantIdCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Obtém o <c>tenantId</c> associado ao slug, ou <c>null</c> se não cacheado.
    /// </summary>
    /// <param name="slug">Slug do tenant.</param>
    /// <param name="ct">Token de cancelamento.</param>
    public async Task<Guid?> GetAsync(string slug, CancellationToken ct = default)
    {
        var cached = await _cache.GetStringAsync(CacheKey(slug), ct);
        if (cached is null)
            return null;

        if (Guid.TryParse(cached, out var tenantId))
            return tenantId;

        _logger.LogWarning("Cache de slug inválido. Slug={Slug} Valor={Valor}", slug, cached);
        return null;
    }

    /// <summary>
    /// Armazena o mapeamento <c>slug → tenantId</c> com TTL padrão.
    /// </summary>
    /// <param name="slug">Slug do tenant.</param>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="ct">Token de cancelamento.</param>
    public async Task SetAsync(string slug, Guid tenantId, CancellationToken ct = default)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DefaultTtl
        };
        await _cache.SetStringAsync(CacheKey(slug), tenantId.ToString(), options, ct);
    }

    /// <summary>
    /// Invalida a entrada de cache para o slug (chamado no provisionamento).
    /// </summary>
    /// <param name="slug">Slug do tenant.</param>
    /// <param name="ct">Token de cancelamento.</param>
    public async Task InvalidateAsync(string slug, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(CacheKey(slug), ct);
        _logger.LogDebug("Cache de slug invalidado. Slug={Slug}", slug);
    }

    private static string CacheKey(string slug) => $"tenant:slug:{slug}";
}
