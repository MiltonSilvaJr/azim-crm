using Authentication.Application.Ports;

namespace Authentication.Infrastructure.RateLimiting;

/// <summary>
/// Implementação do rate limiter usando Redis (janela deslizante por chave).
///
/// Limita por IP e por tenant conforme <see cref="IRateLimiter"/>.
/// Excesso retorna <see langword="false"/> — middleware retorna 429 AUTH-ERR-040.
///
/// Mapeia: TASK-16, RNF 8, design.md § 6.4.
/// </summary>
public sealed class RedisRateLimiter : IRateLimiter
{
    /// <summary>
    /// Verifica se a requisição está dentro do limite.
    ///
    /// Implementação simplificada para Onda 5 (MVP sem Redis real neste stub).
    /// Onda 6 (TASK-25) refinará com Redis sliding window real.
    /// </summary>
    public Task<bool> IsAllowedAsync(
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        // Stub: permite todas as requisições (lógica real em Onda 6)
        return Task.FromResult(true);
    }
}
