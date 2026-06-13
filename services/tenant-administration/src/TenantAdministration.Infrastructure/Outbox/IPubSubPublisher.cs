namespace TenantAdministration.Infrastructure.Outbox;

/// <summary>
/// Abstração para publicação no Google Pub/Sub (design.md §6.3, §6.6).
/// Permite substituição por fake em testes de integração sem dependência de cloud real.
/// </summary>
public interface IPubSubPublisher
{
    /// <summary>
    /// Publica uma mensagem no tópico Pub/Sub configurado.
    /// </summary>
    /// <param name="eventType">Tipo do evento de integração.</param>
    /// <param name="messageId">Identificador da mensagem (idempotência).</param>
    /// <param name="payload">Payload JSON da mensagem.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task PublishAsync(string eventType, Guid messageId, string payload, CancellationToken ct = default);
}
