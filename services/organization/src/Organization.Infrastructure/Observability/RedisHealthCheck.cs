using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Organization.Infrastructure.Observability;

/// <summary>
/// Health check de prontidão para Redis via <see cref="IConnectionMultiplexer"/>.
/// Executa PING para verificar conectividade.
/// Usado no endpoint <c>/health/ready</c>.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    /// <summary>Inicializa o health check com a conexão Redis.</summary>
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
            await db.PingAsync();
            return HealthCheckResult.Healthy("Redis disponível.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Redis indisponível.",
                exception: ex,
                data: new Dictionary<string, object> { ["error"] = ex.GetType().Name });
        }
    }
}
