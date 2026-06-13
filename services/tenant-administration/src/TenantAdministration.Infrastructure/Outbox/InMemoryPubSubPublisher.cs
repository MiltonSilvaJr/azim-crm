namespace TenantAdministration.Infrastructure.Outbox;

/// <summary>
/// Implementação em memória de <see cref="IPubSubPublisher"/> para uso em testes
/// e em ambientes sem acesso ao Google Pub/Sub real.
/// Armazena mensagens publicadas em lista inspecionável.
/// </summary>
public sealed class InMemoryPubSubPublisher : IPubSubPublisher
{
    private readonly List<PublishedMessage> _messages = [];

    /// <summary>Mensagens publicadas (leitura para testes).</summary>
    public IReadOnlyList<PublishedMessage> Messages => _messages.AsReadOnly();

    /// <inheritdoc/>
    public Task PublishAsync(string eventType, Guid messageId, string payload, CancellationToken ct = default)
    {
        _messages.Add(new PublishedMessage(eventType, messageId, payload, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }
}

/// <summary>Mensagem publicada pelo <see cref="InMemoryPubSubPublisher"/>.</summary>
/// <param name="EventType">Tipo do evento.</param>
/// <param name="MessageId">Identificador da mensagem.</param>
/// <param name="Payload">Payload JSON.</param>
/// <param name="PublishedAt">Instante de publicação.</param>
public sealed record PublishedMessage(
    string EventType,
    Guid MessageId,
    string Payload,
    DateTimeOffset PublishedAt);
