using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Digest.Api.Infrastructure;

/// <summary>
/// Health check para conectividade com o tópico Pub/Sub <c>azim-digest-fanout</c> (design §8.2).
/// Em MVP: verifica configuração presente. Em produção: ping ao Pub/Sub SDK.
/// </summary>
public sealed class PubSubHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    /// <summary>Constrói o health check com a configuração do worker.</summary>
    public PubSubHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // MVP: verifica se o tópico está configurado
        var topic = _configuration["PubSub:FanoutTopic"];
        if (string.IsNullOrWhiteSpace(topic))
        {
            return Task.FromResult(
                HealthCheckResult.Degraded("Tópico Pub/Sub 'PubSub:FanoutTopic' não configurado."));
        }

        // Produção: verificar acesso ao tópico com SDK do Pub/Sub (substituir aqui)
        return Task.FromResult(HealthCheckResult.Healthy($"Pub/Sub tópico configurado: {topic}"));
    }
}
