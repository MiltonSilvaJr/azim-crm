namespace Digest.Infrastructure.Messaging;

/// <summary>
/// Registro de mensagem no Outbox transacional (ADR-0004, DD-009).
/// Gravado na mesma transação do <c>UPDATE status='sent'</c> em <c>email_digest_logs</c>.
/// Um relay lê e publica em <c>digest.email_sent.v1</c> no Pub/Sub.
/// Sem PII no payload (RNF 10.2).
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Identificador único da mensagem outbox (deduplicação pelo relay).</summary>
    public Guid Id { get; init; }

    /// <summary>Tipo do evento (ex.: <c>DigestEmailSent</c>).</summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>Payload serializado como JSON (sem PII — RNF 10.2).</summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>Timestamp UTC do evento de domínio.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Timestamp de quando o relay processou e publicou a mensagem.
    /// <see langword="null"/> enquanto pendente.
    /// </summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Identificador do tenant para particionamento de chave Pub/Sub (design §9.1).</summary>
    public Guid TenantId { get; init; }
}
