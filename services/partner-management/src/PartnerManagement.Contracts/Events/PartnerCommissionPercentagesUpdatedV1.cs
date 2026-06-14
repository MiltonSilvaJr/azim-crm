namespace PartnerManagement.Contracts.Events;

/// <summary>
/// Envelope de evento de integração <c>partner.commission_percentages_updated.v1</c>.
/// Publicado quando os percentuais padrão de comissão são alterados.
/// Carga sem PII: apenas <c>partnerId</c>, <c>tenantId</c> e campos alterados (RNF 4).
/// Mapeia: design §9, Req 2.5, TASK-22.
/// </summary>
public sealed class PartnerCommissionPercentagesUpdatedV1
{
    /// <summary>Identificador único do evento.</summary>
    public Guid EventId { get; init; }

    /// <summary>Versão do contrato (sempre <c>"v1"</c>).</summary>
    public string EventVersion { get; init; } = "v1";

    /// <summary>Identificador do parceiro alterado.</summary>
    public Guid PartnerId { get; init; }

    /// <summary>Tenant ao qual o parceiro pertence.</summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Campos de percentual que foram alterados (ex.: <c>["pct_setup"]</c>, <c>["pct_recorrente"]</c>).
    /// Não inclui os valores em si para evitar vazamento de dados contratuais sensíveis.
    /// </summary>
    public IReadOnlyList<string> ChangedFields { get; init; } = [];

    /// <summary>Momento em que o evento ocorreu (UTC).</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Identificador de correlação da requisição originadora.</summary>
    public string? CorrelationId { get; init; }
}
