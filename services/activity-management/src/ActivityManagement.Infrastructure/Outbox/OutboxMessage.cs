namespace ActivityManagement.Infrastructure.Outbox;

/// <summary>
/// Mensagem do Outbox transacional.
/// Persistida na mesma transação da escrita de domínio e relayada ao Cloud Pub/Sub
/// pelo <see cref="OutboxRelayWorker"/> em background (design §6.6, DD-007).
/// Chave de deduplicação <see cref="DedupKey"/> previne duplicatas dentro do mesmo scan
/// para <c>ActivityOverdue</c> — índice único <c>uq_outbox_dedup</c> (design §7).
/// Mapeia: design §6.5, §6.6, §7, TASK-13.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Identificador da mensagem (UUID, PK).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Tenant ao qual o evento pertence (obrigatório).</summary>
    public Guid TenantId { get; init; }

    /// <summary>Tipo do evento (<c>ActivityCreated</c>, <c>ActivityCompleted</c>, <c>ActivityOverdue</c>).</summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// Chave de deduplicação opcional.
    /// Para <c>ActivityOverdue</c>: <c>(activityId, scanDate)</c> — design §7, DD-005.
    /// Índice único impede duplicata na mesma janela.
    /// </summary>
    public string? DedupKey { get; init; }

    /// <summary>Payload serializado em JSON sem PII (sem título/descrição — RNF 7.2).</summary>
    public string PayloadJson { get; init; } = string.Empty;

    /// <summary>Instante em que o evento ocorreu.</summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Instante em que o evento foi publicado no Pub/Sub.
    /// Nulo enquanto pendente de publicação.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }
}
