using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Authentication.Infrastructure.HealthChecks;

/// <summary>
/// Health check do Redis (Memorystore).
///
/// Usado no endpoint <c>GET /health/ready</c>. Executa um PING ao Redis e verifica
/// a latência de resposta. Latência acima de 2 s é considerada indisponibilidade
/// para fins de readiness (RNF 3.2, design.md § 11).
///
/// Nunca expõe detalhe interno (stack trace, mensagem de exceção do cliente Redis)
/// na resposta HTTP (security).
///
/// Mapeia: TASK-21, RNF 3.2, design.md § 11.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    /// <summary>Latência máxima aceitável para considerar o Redis disponível.</summary>
    private static readonly TimeSpan MaxAcceptableLatency = TimeSpan.FromSeconds(2);

    private readonly IConnectionMultiplexer _redis;

    /// <summary>
    /// Inicializa o health check com a conexão Redis.
    /// </summary>
    /// <param name="redis">Multiplexer de conexão Redis.</param>
    public RedisHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var latency = await db.PingAsync();

            if (latency > MaxAcceptableLatency)
            {
                return HealthCheckResult.Unhealthy(
                    $"Redis com latência elevada: {latency.TotalMilliseconds:F0} ms.");
            }

            return HealthCheckResult.Healthy();
        }
        catch
        {
            // Nunca propaga exceção nem expõe mensagem interna do cliente Redis
            return HealthCheckResult.Unhealthy("Redis não disponível.");
        }
    }
}
