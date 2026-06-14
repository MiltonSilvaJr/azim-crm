namespace Organization.Infrastructure.Persistence;

/// <summary>
/// Entidade de persistência para o padrão Outbox.
/// Registros escritos na mesma transação do agregado; publicados pelo <see cref="OutboxWorker"/>.
/// Tabela: <c>outbox_events</c>.
/// </summary>
public sealed class OutboxEvent
{
    /// <summary>Identificador único do registro de Outbox.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Identificador do tenant dono do evento.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Tipo do evento de integração (ex.: <c>user.activated.v1</c>).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Payload JSON do evento (sem PII de acordo com DD-005).</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Identificador de correlação para rastreabilidade (ADR-0009).</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Identificador de causalidade (opcional).</summary>
    public Guid? CausationId { get; set; }

    /// <summary>Instante em que o evento ocorreu no domínio.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Instante de publicação para o Pub/Sub. Nulo até a publicação ser confirmada.</summary>
    public DateTimeOffset? PublishedAt { get; set; }
}
