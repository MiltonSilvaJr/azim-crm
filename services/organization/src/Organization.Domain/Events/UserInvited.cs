namespace Organization.Domain.Events;

/// <summary>
/// Domain event emitido quando um convite de usuário é criado.
/// Carga sem PII: apenas identificadores (correlação sem e-mail).
/// Consumido pelo <c>audit-log</c> para registrar o ato do convite.
/// </summary>
public sealed record UserInvited(
    Guid TenantId,
    Guid InvitationId,
    DateTimeOffset OccurredAt) : IDomainEvent;
