namespace Organization.Domain.Events;

/// <summary>
/// Domain event emitido quando um usuário é ativado (aceite de convite ou provisionamento inicial).
/// Carga sem PII: apenas identificadores e memberships (buId → role).
/// Consumido pelo módulo <c>authentication</c> para carregar memberships no token.
/// </summary>
public sealed record UserActivated(
    Guid TenantId,
    Guid UserId,
    IReadOnlyList<(Guid BuId, string Role)> Memberships,
    DateTimeOffset OccurredAt) : IDomainEvent;
