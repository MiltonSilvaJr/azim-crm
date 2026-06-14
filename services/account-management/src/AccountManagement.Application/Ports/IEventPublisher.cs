using AccountManagement.Domain.Shared;

namespace AccountManagement.Application.Ports;

/// <summary>
/// Porta de saída para publicação de domain events após commit transacional.
///
/// Implementação concreta na Infrastructure (Outbox + Cloud Pub/Sub — DD-007).
/// Declarada na Application para manter a dependência correta (Application → Domain;
/// Infrastructure → Application — design §3).
///
/// Mapeia: design §5.3, DD-007, Req 8.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publica um domain event de forma assíncrona.
    /// A implementação concreta grava no Outbox na mesma transação (DD-007).
    /// </summary>
    /// <param name="domainEvent">Evento a ser publicado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
