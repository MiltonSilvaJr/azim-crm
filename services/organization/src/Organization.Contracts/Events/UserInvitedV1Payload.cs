namespace Organization.Contracts.Events;

/// <summary>
/// Carga do evento de integração <c>user.invited.v1</c>.
/// Publicado quando um convite de usuário é criado.
/// Sem PII: apenas identificadores — sem e-mail ou <c>display_name</c> (RNF 3, DD-005).
/// </summary>
/// <param name="TenantId">Identificador do tenant dono do convite.</param>
/// <param name="InvitationId">Identificador único do convite.</param>
public sealed record UserInvitedV1Payload(
    Guid TenantId,
    Guid InvitationId);
