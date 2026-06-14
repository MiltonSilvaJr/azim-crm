namespace Organization.Domain.Events;

/// <summary>
/// Domain event emitido quando uma Business Unit é criada com sucesso.
/// Carga sem PII (apenas identificadores e nome da BU).
/// </summary>
public sealed record BusinessUnitCreated(
    Guid TenantId,
    Guid BusinessUnitId,
    string Name,
    DateTimeOffset OccurredAt) : IDomainEvent;
