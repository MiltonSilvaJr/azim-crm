namespace TenantAdministration.Domain.Events;

/// <summary>
/// Evento de domínio emitido quando um tenant é suspenso.
/// Mapeado para evento de integração <c>tenant.suspended.v1</c>.
/// </summary>
public sealed record TenantSuspended(
    Guid TenantId,
    string Slug,
    DateTimeOffset SuspendedAt) : IDomainEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; } = Guid.NewGuid();

    /// <inheritdoc/>
    public DateTimeOffset OccurredAt => SuspendedAt;
}
