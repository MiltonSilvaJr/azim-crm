namespace Organization.Domain.Events;

/// <summary>
/// Domain event emitido quando um usuário é desativado (soft-delete).
/// Carga sem PII: apenas identificadores.
/// Consumido pelo módulo <c>authentication</c> para invalidar cache.
/// </summary>
public sealed record UserDeactivated(
    Guid TenantId,
    Guid UserId,
    DateTimeOffset OccurredAt) : IDomainEvent;
