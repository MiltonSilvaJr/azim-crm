namespace PartnerManagement.Contracts.Events;

/// <summary>
/// Envelope de evento de integração <c>partner.deactivated.v1</c>.
/// Publicado quando um parceiro é inativado com transição efetiva (DD-006).
/// Carga sem PII (RNF 4): apenas <c>partnerId</c> e <c>tenantId</c>.
/// Mapeia: design §9, Req 3.6, TASK-22.
/// </summary>
public sealed class PartnerDeactivatedV1
{
    /// <summary>Identificador único do evento.</summary>
    public Guid EventId { get; init; }

    /// <summary>Versão do contrato (sempre <c>"v1"</c>).</summary>
    public string EventVersion { get; init; } = "v1";

    /// <summary>Identificador do parceiro inativado.</summary>
    public Guid PartnerId { get; init; }

    /// <summary>Tenant ao qual o parceiro pertence.</summary>
    public Guid TenantId { get; init; }

    /// <summary>Momento em que o evento ocorreu (UTC).</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Identificador de correlação da requisição originadora.</summary>
    public string? CorrelationId { get; init; }
}
