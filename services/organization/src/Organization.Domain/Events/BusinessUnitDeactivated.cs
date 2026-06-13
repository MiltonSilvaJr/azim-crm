namespace Organization.Domain.Events;

/// <summary>
/// Domain event emitido quando uma Business Unit é desativada (soft-delete).
/// </summary>
public sealed record BusinessUnitDeactivated(
    Guid TenantId,
    Guid BusinessUnitId,
    DateTimeOffset OccurredAt) : IDomainEvent;
