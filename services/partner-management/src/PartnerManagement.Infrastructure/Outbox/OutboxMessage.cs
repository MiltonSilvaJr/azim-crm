namespace PartnerManagement.Infrastructure.Outbox;

/// <summary>
/// Mensagem do Outbox transacional persistida junto com a escrita de domínio.
/// O relay (<see cref="OutboxPublisher"/>) lê mensagens pendentes e publica no Pub/Sub.
/// Garante atomicidade estado↔evento e entrega at-least-once (design §6.6, RNF 2).
/// Mapeia: RNF 2, design §6.3, design §6.6, TASK-18.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Identificador único da mensagem de Outbox.</summary>
    public Guid Id { get; private init; }

    /// <summary>Tenant ao qual a mensagem pertence (RLS, DD-001).</summary>
    public Guid TenantId { get; private init; }

    /// <summary>Tipo do evento de domínio (ex.: "PartnerCreated").</summary>
    public string EventType { get; private init; } = null!;

    /// <summary>
    /// Payload JSON do evento sem PII em claro (RNF 4, DD-008).
    /// Mascarado por <c>PartnerPiiMasker</c> antes de persistir.
    /// </summary>
    public string PayloadJson { get; private init; } = null!;

    /// <summary>Instante em que o evento ocorreu (UTC).</summary>
    public DateTimeOffset OccurredAt { get; private init; }

    /// <summary>
    /// Instante em que a mensagem foi publicada no Pub/Sub.
    /// <c>null</c> indica que ainda não foi publicada (pendente).
    /// </summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    // EF Core: construtor privado para rehidratação
    private OutboxMessage()
    {
    }

    /// <summary>
    /// Cria uma nova mensagem de Outbox.
    /// </summary>
    /// <param name="tenantId">Tenant da operação.</param>
    /// <param name="eventType">Tipo do evento.</param>
    /// <param name="payloadJson">Payload JSON (sem PII em claro).</param>
    /// <param name="occurredAt">Instante do evento (UTC).</param>
    public static OutboxMessage Create(
        Guid tenantId,
        string eventType,
        string payloadJson,
        DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = eventType,
            PayloadJson = payloadJson,
            OccurredAt = occurredAt
        };
    }

    /// <summary>
    /// Marca a mensagem como publicada com o instante informado.
    /// Chamado pelo relay (<see cref="OutboxPublisher"/>) após publicação no Pub/Sub.
    /// </summary>
    /// <param name="publishedAt">Instante da publicação (UTC).</param>
    public void MarkAsPublished(DateTimeOffset publishedAt)
    {
        PublishedAt = publishedAt;
    }
}
