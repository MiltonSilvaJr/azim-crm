using Microsoft.Extensions.Logging;

namespace Organization.Infrastructure.Messaging;

/// <summary>
/// Implementação stub de <see cref="IPubSubPublisher"/> para MVP e testes locais.
/// Loga a intenção de publicação sem enviar para o Pub/Sub real.
/// Substituível por <c>GcpPubSubPublisher</c> em produção via DI.
/// </summary>
public sealed class LoggingPubSubPublisher : IPubSubPublisher
{
    private readonly ILogger<LoggingPubSubPublisher> _logger;

    /// <summary>Inicializa o publisher stub.</summary>
    public LoggingPubSubPublisher(ILogger<LoggingPubSubPublisher> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task PublishAsync(
        string topic,
        string payload,
        IReadOnlyDictionary<string, string> attributes,
        CancellationToken cancellationToken = default)
    {
        // MVP stub: loga a publicação sem enviar ao broker real.
        // Em produção, substituir por GcpPubSubPublisher via DI.
        // Não loga o payload completo para evitar PII acidental.
        _logger.LogInformation(
            "PubSub [stub] publicado — topic: {Topic}, correlation_id: {CorrelationId}, tenant_id: {TenantId}.",
            topic,
            attributes.GetValueOrDefault("correlation_id", "n/a"),
            attributes.GetValueOrDefault("tenant_id", "n/a"));

        return Task.CompletedTask;
    }
}
