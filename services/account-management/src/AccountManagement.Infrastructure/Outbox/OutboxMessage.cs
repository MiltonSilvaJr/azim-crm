namespace AccountManagement.Infrastructure.Outbox;

/// <summary>
/// Entidade de persistência para a tabela <c>outbox_messages</c> do padrão Outbox transacional.
///
/// Mensagens são gravadas na mesma transação da escrita de domínio pelo
/// <c>TransactionBehavior</c> e publicadas assincronamente pelo <c>OutboxPublisher</c>
/// no Cloud Pub/Sub com entrega at-least-once (DD-007).
///
/// O payload nunca contém PII em texto claro — garantido pelo <c>PiiMasker</c> (DD-003).
///
/// Mapeia: design §7 (outbox_messages), design §6.6, DD-007, TASK-09.
/// </summary>
internal sealed class OutboxMessage
{
    /// <summary>Identificador único da mensagem (UUID — para deduplicação no consumidor).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Tenant do evento (isolamento multi-tenant no envelope).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Tipo do evento (ex.: <c>account.created.v1</c>).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Payload JSON serializado do evento de domínio.
    /// PII mascarada pelo <c>PiiMasker</c> antes de gravar (DD-003).
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>Momento em que o evento ocorreu no domínio (UTC).</summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Momento de publicação no Pub/Sub. <c>null</c> quando ainda não publicado.
    /// Preenchido apenas após confirmação do broker (DD-007).
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }
}
