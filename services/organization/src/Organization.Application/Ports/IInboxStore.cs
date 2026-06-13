namespace Organization.Application.Ports;

/// <summary>
/// Port de saída para deduplicação de mensagens consumidas (padrão Inbox).
/// Persiste em <c>inbox_messages</c> na mesma transação do processamento (§6.6).
/// </summary>
public interface IInboxStore
{
    /// <summary>
    /// Verifica se a mensagem já foi processada.
    /// </summary>
    /// <param name="messageId">Identificador único da mensagem.</param>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns><c>true</c> quando a mensagem já foi processada; <c>false</c> caso contrário.</returns>
    Task<bool> IsProcessedAsync(string messageId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra a mensagem como processada.
    /// </summary>
    /// <param name="messageId">Identificador único da mensagem.</param>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="eventType">Tipo do evento para rastreabilidade.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task MarkProcessedAsync(
        string messageId,
        Guid tenantId,
        string eventType,
        CancellationToken cancellationToken = default);
}
