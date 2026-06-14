using Microsoft.Extensions.Caching.Memory;
using OpportunityPipeline.Application.Behaviors;

namespace OpportunityPipeline.Infrastructure.DI;

/// <summary>
/// Implementação in-memory do IIdempotencyStore para desenvolvimento e testes.
/// Em produção, substituir por RedisIdempotencyStore (Redis com TTL de 24 h).
/// TTL padrão: 24 horas.
/// Mapeia: NFR-RES-02, design §6.5, TASK-20.
/// </summary>
public sealed class InMemoryIdempotencyStore(IMemoryCache cache) : IIdempotencyStore
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    /// <inheritdoc/>
    public Task<string?> TryGetAsync(string compositeKey, CancellationToken cancellationToken = default)
    {
        cache.TryGetValue(compositeKey, out string? value);
        return Task.FromResult(value);
    }

    /// <inheritdoc/>
    public Task SetAsync(string compositeKey, string serializedResponse, CancellationToken cancellationToken = default)
    {
        cache.Set(compositeKey, serializedResponse, DefaultTtl);
        return Task.CompletedTask;
    }
}
