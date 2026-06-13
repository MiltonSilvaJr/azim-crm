namespace TenantAdministration.Domain.Events;

/// <summary>
/// Evento de domínio emitido quando um tenant é reativado.
/// Mapeado para evento de integração <c>tenant.reactivated.v1</c>.
/// </summary>
public sealed record TenantReactivated(
    Guid TenantId,
    string Slug,
    DateTimeOffset ReactivatedAt) : IDomainEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; } = Guid.NewGuid();

    /// <inheritdoc/>
    public DateTimeOffset OccurredAt => ReactivatedAt;
}
