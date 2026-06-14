namespace PartnerManagement.Domain.Partners.Events;

/// <summary>
/// Evento de domínio: parceiro reativado (transição efetiva).
/// Emitido apenas quando há transição real de Inactive → Active.
/// Em transição idempotente (já ativo), nenhum evento é emitido (DD-006, PBT-02).
/// Carga sem PII (RNF 4, design §4.4).
/// Mapeia: Req 3.6, design §4.4.
/// </summary>
/// <param name="PartnerId">Identificador do parceiro.</param>
/// <param name="TenantId">Identificador do tenant.</param>
/// <param name="OccurredAt">Momento em que o evento ocorreu (UTC).</param>
public sealed record PartnerReactivated(
    Guid PartnerId,
    Guid TenantId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; } = Guid.NewGuid();
}
