namespace TenantAdministration.Infrastructure.Outbox;

/// <summary>
/// Status de publicação de um evento no Outbox.
/// </summary>
public enum OutboxEventStatus
{
    Pending,
    Published,
    Failed
}

/// <summary>
/// Entidade que representa um evento pendente de publicação no Pub/Sub.
/// Armazenado na tabela <c>outbox_events</c> dentro da mesma transação do agregado
/// (design.md §6.6, Req 11.4). Envelope conforme TRD §9.2.
/// </summary>
public sealed class OutboxEvent
{
    /// <summary>Identificador único do evento (event_id do envelope TRD).</summary>
    public Guid Id { get; init; }

    /// <summary>Tipo do evento de integração (ex.: <c>tenant.provisioned.v1</c>).</summary>
    public string EventType { get; init; } = default!;

    /// <summary>Tipo do agregado que originou o evento (ex.: <c>Tenant</c>).</summary>
    public string AggregateType { get; init; } = default!;

    /// <summary>Identificador do agregado.</summary>
    public Guid AggregateId { get; init; }

    /// <summary>ID do tenant para isolamento no envelope.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>ID de correlação do request originador.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Payload serializado em JSON (design.md §9.1).</summary>
    public string Payload { get; init; } = default!;

    /// <summary>Status de publicação.</summary>
    public OutboxEventStatus Status { get; set; } = OutboxEventStatus.Pending;

    /// <summary>Data de criação do registro no Outbox.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Data de publicação bem-sucedida no Pub/Sub.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Número de tentativas de publicação falhas.</summary>
    public int RetryCount { get; set; }

    /// <summary>Última mensagem de erro de publicação.</summary>
    public string? LastError { get; set; }
}
