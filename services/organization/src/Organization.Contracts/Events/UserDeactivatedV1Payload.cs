namespace Organization.Contracts.Events;

/// <summary>
/// Carga do evento de integração <c>user.deactivated.v1</c>.
/// Publicado quando um usuário é desativado (soft-delete).
/// Sem PII: apenas identificadores — sem e-mail ou <c>display_name</c> (RNF 3, DD-005).
/// Consumido pelo módulo <c>authentication</c> para invalidar cache.
/// </summary>
/// <param name="TenantId">Identificador do tenant dono do usuário.</param>
/// <param name="UserId">Identificador único do usuário desativado.</param>
public sealed record UserDeactivatedV1Payload(
    Guid TenantId,
    Guid UserId);
