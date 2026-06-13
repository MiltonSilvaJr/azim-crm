namespace TenantAdministration.Domain.Events;

/// <summary>
/// Evento de domínio emitido quando a configuração de fuso/horário do digest é atualizada.
/// Evento interno: sem publicação externa no MVP (design.md §4.4).
/// </summary>
public sealed record DigestConfigChanged(
    Guid TenantId,
    string Timezone,
    string DigestTime,
    DateTimeOffset ChangedAt) : IDomainEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; } = Guid.NewGuid();

    /// <inheritdoc/>
    public DateTimeOffset OccurredAt => ChangedAt;
}
