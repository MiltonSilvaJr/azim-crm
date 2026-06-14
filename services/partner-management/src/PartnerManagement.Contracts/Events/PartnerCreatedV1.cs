namespace PartnerManagement.Contracts.Events;

/// <summary>
/// Envelope de evento de integração <c>partner.created.v1</c>.
/// Publicado via Outbox + Cloud Pub/Sub após criação de parceiro.
/// Carga sem PII em claro: sem <c>name</c>, <c>contact_email</c>, <c>contact_phone</c> (RNF 4, DD-008).
/// Mapeia: design §9, Req 1.8, RNF 2.4, TASK-22.
/// </summary>
public sealed class PartnerCreatedV1
{
    /// <summary>Identificador único do evento (para deduplicação por <c>event_id</c>).</summary>
    public Guid EventId { get; init; }

    /// <summary>Versão do contrato (sempre <c>"v1"</c> para este tipo).</summary>
    public string EventVersion { get; init; } = "v1";

    /// <summary>Identificador do parceiro criado.</summary>
    public Guid PartnerId { get; init; }

    /// <summary>Tenant ao qual o parceiro pertence.</summary>
    public Guid TenantId { get; init; }

    /// <summary>Papel tipado canônico do parceiro (não é PII).</summary>
    public string PartnerType { get; init; } = null!;

    /// <summary>Momento em que o evento ocorreu (UTC).</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Identificador de correlação da requisição originadora.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Identificador de causação (requisição que causou o evento).</summary>
    public string? CausationId { get; init; }
}
