namespace Organization.Infrastructure.Messaging;

/// <summary>
/// Port interna de Infrastructure para publicação de mensagens em Pub/Sub.
/// Permite substituição por implementação real (GCP Pub/Sub) sem alterar o <see cref="OutboxWorker"/>.
/// Envelope padrão: evento serializado + tenant_id + correlation_id + causation_id.
/// </summary>
public interface IPubSubPublisher
{
    /// <summary>
    /// Publica uma mensagem para o tópico de destino.
    /// </summary>
    /// <param name="topic">Nome do tópico.</param>
    /// <param name="payload">Payload JSON serializado do evento.</param>
    /// <param name="attributes">Atributos de roteamento e rastreabilidade (tenant_id, correlation_id, etc.).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task PublishAsync(
        string topic,
        string payload,
        IReadOnlyDictionary<string, string> attributes,
        CancellationToken cancellationToken = default);
}
