using TenantAdministration.Domain.Events;

namespace TenantAdministration.Application.Ports;

/// <summary>
/// Porta de saída para o padrão Outbox (design.md §6.6, Req 11.4).
/// Persiste eventos de domínio na tabela <c>outbox_events</c> dentro da mesma
/// transação do agregado (garantida pelo <c>TransactionBehavior</c>).
/// Implementação concreta vem na Onda 4 (Infrastructure).
/// </summary>
public interface IEventOutbox
{
    /// <summary>
    /// Adiciona um domain event à fila do Outbox dentro da transação corrente.
    /// </summary>
    /// <param name="domainEvent">Evento de domínio a enfileirar.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task AppendAsync(IDomainEvent domainEvent, CancellationToken ct = default);

    /// <summary>
    /// Adiciona múltiplos domain events à fila do Outbox.
    /// </summary>
    /// <param name="domainEvents">Eventos de domínio a enfileirar.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task AppendRangeAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default);
}
