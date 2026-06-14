namespace Organization.Domain.Events;

/// <summary>
/// Domain event emitido quando o papel de um membership é alterado.
/// Carga sem PII: apenas identificadores e novo papel.
/// Consumido pelo módulo <c>authentication</c> para invalidar cache de memberships.
/// </summary>
public sealed record MembershipRoleChanged(
    Guid TenantId,
    Guid UserId,
    Guid BuId,
    string Role,
    DateTimeOffset OccurredAt) : IDomainEvent;
