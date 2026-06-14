namespace Organization.Contracts.Events;

/// <summary>
/// Carga do evento de integração <c>user.role_changed.v1</c>.
/// Publicado quando o papel de um membership é alterado.
/// Sem PII: apenas identificadores e novo papel — sem e-mail ou <c>display_name</c> (RNF 3, DD-005).
/// Consumido pelo módulo <c>authentication</c> para invalidar cache de memberships.
/// </summary>
/// <param name="TenantId">Identificador do tenant dono do usuário.</param>
/// <param name="UserId">Identificador único do usuário cujo papel foi alterado.</param>
/// <param name="BuId">Identificador da Business Unit do membership alterado.</param>
/// <param name="Role">Novo papel atribuído (ex.: <c>GestorBU</c>).</param>
public sealed record UserRoleChangedV1Payload(
    Guid TenantId,
    Guid UserId,
    Guid BuId,
    string Role);
