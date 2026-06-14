namespace PartnerManagement.Domain.Partners.Events;

/// <summary>
/// Evento de domínio: parceiro criado.
/// Disparado quando um parceiro é criado pela primeira vez.
/// Carga sem PII: contém apenas <c>partnerId</c>, <c>tenantId</c>, <c>partnerType</c>
/// e <c>occurredAt</c> (RNF 4, design §4.4).
/// Publicado como contrato de integração <c>partner.created.v1</c> ao audit-log via Outbox.
/// Mapeia: Req 1.8, RNF 2.4, design §4.4.
/// </summary>
/// <param name="PartnerId">Identificador do parceiro criado.</param>
/// <param name="TenantId">Identificador do tenant.</param>
/// <param name="PartnerType">Papel canônico do parceiro (sem nome — RNF 4).</param>
/// <param name="OccurredAt">Momento em que o evento ocorreu (UTC).</param>
public sealed record PartnerCreated(
    Guid PartnerId,
    Guid TenantId,
    string PartnerType,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; } = Guid.NewGuid();
}
