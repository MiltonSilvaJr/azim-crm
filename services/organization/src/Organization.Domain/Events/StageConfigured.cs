namespace Organization.Domain.Events;

/// <summary>
/// Domain event emitido quando um estágio é adicionado, renomeado ou reordenado.
/// </summary>
public sealed record StageConfigured(
    Guid TenantId,
    Guid BusinessUnitId,
    Guid StageId,
    DateTimeOffset OccurredAt) : IDomainEvent;
