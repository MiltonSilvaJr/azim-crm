namespace Organization.Infrastructure.Persistence;

/// <summary>
/// Entidade de persistência para o padrão Inbox (deduplicação de mensagens consumidas).
/// Tabela: <c>inbox_messages</c>.
/// </summary>
public sealed class InboxMessage
{
    /// <summary>Identificador único da mensagem consumida (chave de deduplicação).</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Identificador do tenant.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Tipo do evento consumido para rastreabilidade.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Instante em que a mensagem foi processada.</summary>
    public DateTimeOffset ProcessedAt { get; set; }
}
