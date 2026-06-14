using System.Text.Json.Serialization;

namespace Digest.Contracts.Events;

/// <summary>
/// Evento de integração <c>digest.email_sent.v1</c> publicado no tópico <c>azim-digest</c> (design §9.1).
/// Gerado via Outbox transacional após envio aceito pelo provedor (ADR-0004, DD-009).
///
/// Campos exatamente conforme design §9.1:
/// <c>event</c>, <c>version</c>, <c>tenant_id</c>, <c>user_id</c>, <c>digest_date</c>,
/// <c>message_id</c>, <c>correlation_id</c>, <c>causation_id</c>, <c>occurred_at</c>.
///
/// Sem PII: nenhum campo de e-mail, nome do usuário ou conteúdo do digest (RNF 10.2, RNF 3.4).
/// Versionamento por sufixo <c>.v1</c>; compatibilidade retroativa aditiva (design §9.1).
/// </summary>
public sealed class DigestEmailSentEvent
{
    /// <summary>
    /// Nome canônico do evento. Sempre <c>"digest.email_sent.v1"</c>.
    /// Versionamento por sufixo: campo aditivo não quebra consumidores existentes.
    /// </summary>
    [JsonPropertyName("event")]
    public string Event { get; init; } = "digest.email_sent.v1";

    /// <summary>Versão do schema do evento. Sempre <c>"1"</c> para esta versão.</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1";

    /// <summary>Identificador do tenant. Sem PII.</summary>
    [JsonPropertyName("tenant_id")]
    public Guid TenantId { get; init; }

    /// <summary>Identificador opaco do usuário destinatário. Sem PII.</summary>
    [JsonPropertyName("user_id")]
    public Guid UserId { get; init; }

    /// <summary>
    /// Data local do digest no fuso do tenant (formato <c>yyyy-MM-dd</c>).
    /// Nunca em UTC: data local do tenant (Req 8.3, design §4.3).
    /// </summary>
    [JsonPropertyName("digest_date")]
    public string DigestDate { get; init; } = string.Empty;

    /// <summary>
    /// Identificador da mensagem no provedor de e-mail (sem PII).
    /// Usado para correlação com eventos de entrega (Req 11.1).
    /// </summary>
    [JsonPropertyName("message_id")]
    public string MessageId { get; init; } = string.Empty;

    /// <summary>Identificador de correlação da execução do digest (sem PII).</summary>
    [JsonPropertyName("correlation_id")]
    public Guid? CorrelationId { get; init; }

    /// <summary>
    /// Identificador de causalidade — referência ao <c>RunDigestForTenantCommand</c>
    /// que originou o envio (sem PII). Opcional no MVP.
    /// </summary>
    [JsonPropertyName("causation_id")]
    public Guid? CausationId { get; init; }

    /// <summary>Instante UTC em que o evento ocorreu (envio aceito pelo provedor).</summary>
    [JsonPropertyName("occurred_at")]
    public DateTimeOffset OccurredAt { get; init; }
}
