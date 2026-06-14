using Organization.Domain.Events;

namespace Organization.Application.Ports;

/// <summary>
/// Port de saída para enfileirar domain events na tabela <c>outbox_events</c>.
/// A escrita ocorre na mesma transação do agregado, garantindo atomicidade evento↔estado (DD-004, §6.6).
/// </summary>
public interface IEventOutbox
{
    /// <summary>
    /// Enfileira um domain event no Outbox dentro da transação corrente.
    /// </summary>
    /// <param name="domainEvent">Evento de domínio a publicar.</param>
    /// <param name="tenantId">Identificador do tenant dono do evento.</param>
    /// <param name="correlationId">Identificador de correlação para rastreabilidade (ADR-0009).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task EnqueueAsync(
        IDomainEvent domainEvent,
        Guid tenantId,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}
