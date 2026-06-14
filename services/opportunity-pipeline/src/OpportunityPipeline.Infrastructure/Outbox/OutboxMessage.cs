namespace OpportunityPipeline.Infrastructure.Outbox;

/// <summary>
/// Mensagem do Outbox — envelope que carrega um evento de domínio serializado.
/// Gravada na mesma transação do estado de negócio; publicada no Pub/Sub pelo OutboxPublisher.
/// Mapeia: ADR-0004, design §6.6, TASK-13 (schema), TASK-16 (publisher).
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Identificador único da mensagem.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant dono da mensagem (RLS).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Tipo do evento (ex.: "opportunity.created.v1").</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Payload serializado em JSON.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Status: "pending" | "published" | "failed".</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Data/hora de criação.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Data/hora de publicação (null enquanto pendente).</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Número de tentativas de publicação.</summary>
    public int Attempts { get; set; }

    /// <summary>Último erro de publicação (para DLQ).</summary>
    public string? LastError { get; set; }
}
