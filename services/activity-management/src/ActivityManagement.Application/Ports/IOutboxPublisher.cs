namespace ActivityManagement.Application.Ports;

using ActivityManagement.Domain.Activities.Events;

/// <summary>
/// Porta para publicação de eventos no Outbox transacional (design §6.5, Req 14, DD-005).
/// A implementação persiste o evento no Outbox na mesma transação da escrita de negócio.
/// O relay do Outbox para o Cloud Pub/Sub é responsabilidade da Infrastructure.
/// Mapeia: design §6.5, DD-004, DD-005, Req 14, TASK-12.
/// </summary>
public interface IOutboxPublisher
{
    /// <summary>
    /// Enfileira um evento de domínio no Outbox com chave de deduplicação opcional.
    /// </summary>
    /// <param name="domainEvent">Evento a enfileirar.</param>
    /// <param name="deduplicationKey">
    /// Chave de deduplicação; quando fornecida, o evento não é enfileirado novamente
    /// se a chave já existir na janela de deduplicação (DD-005).
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task EnqueueAsync(
        DomainEvent       domainEvent,
        string?           deduplicationKey  = null,
        CancellationToken cancellationToken = default);
}
