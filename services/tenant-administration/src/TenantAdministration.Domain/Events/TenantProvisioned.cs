namespace TenantAdministration.Domain.Events;

/// <summary>
/// Evento de domínio emitido quando um tenant é provisionado com sucesso.
/// Payload mínimo conforme design.md §4.4.
/// Mapeado para evento de integração <c>tenant.provisioned.v1</c>.
/// </summary>
public sealed record TenantProvisioned(
    Guid TenantId,
    string Slug,
    string DisplayName,
    string Timezone,
    string DigestTime,
    DateTimeOffset ProvisionedAt) : IDomainEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; } = Guid.NewGuid();

    /// <inheritdoc/>
    public DateTimeOffset OccurredAt => ProvisionedAt;
}
