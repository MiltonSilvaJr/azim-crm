namespace TenantAdministration.Domain.Events;

/// <summary>
/// Evento de domínio emitido quando o branding do tenant é atualizado.
/// Mapeado para evento de integração <c>tenant.branding_changed.v1</c>.
/// </summary>
public sealed record BrandingChanged(
    Guid TenantId,
    string Slug,
    bool WcagContrastOk,
    DateTimeOffset ChangedAt) : IDomainEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; } = Guid.NewGuid();

    /// <inheritdoc/>
    public DateTimeOffset OccurredAt => ChangedAt;
}
