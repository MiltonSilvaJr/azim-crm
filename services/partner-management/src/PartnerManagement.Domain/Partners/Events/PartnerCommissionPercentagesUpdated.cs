namespace PartnerManagement.Domain.Partners.Events;

/// <summary>
/// Evento de domínio: percentuais padrão de comissão do parceiro foram alterados.
/// Disparado quando <c>pct_setup</c> e/ou <c>pct_recorrente</c> são modificados.
/// Carga sem PII: sem <c>name</c> nem contato (RNF 4, design §4.4).
/// Mapeia: Req 2.5, design §4.4.
/// </summary>
/// <param name="PartnerId">Identificador do parceiro.</param>
/// <param name="TenantId">Identificador do tenant.</param>
/// <param name="ChangedFields">Nomes dos campos alterados (ex.: ["pct_setup", "pct_recorrente"]).</param>
/// <param name="OccurredAt">Momento em que o evento ocorreu (UTC).</param>
public sealed record PartnerCommissionPercentagesUpdated(
    Guid PartnerId,
    Guid TenantId,
    IReadOnlyList<string> ChangedFields,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; } = Guid.NewGuid();
}
