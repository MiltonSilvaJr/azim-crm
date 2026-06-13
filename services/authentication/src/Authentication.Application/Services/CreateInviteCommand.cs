namespace Authentication.Application.Services;

/// <summary>
/// Comando para criar um convite de ativação para novo usuário no tenant.
///
/// Handler: <see cref="InviteActivationService.CreateAsync"/>.
///
/// Gera link de ativação via <c>IIdentityProvider</c> (TTL 72h — DD-009)
/// e dispara e-mail via <c>IEmailSender</c>.
/// Falha de e-mail → <see cref="InviteEmailFailedException"/> (AUTH-ERR-032)
/// sem reverter o convite criado no IdP (Req 7.2).
///
/// Mapeia: design.md § 5.1, Req 7, DD-009.
/// </summary>
/// <param name="Email">E-mail do usuário convidado.</param>
/// <param name="FirebaseTenant">Tenant de identidade no IdP.</param>
/// <param name="TenantId">UUID do tenant Azim.</param>
/// <param name="InviterId">UUID do usuário que gerou o convite (Tenant Admin).</param>
public sealed record CreateInviteCommand(
    string Email,
    string FirebaseTenant,
    Guid TenantId,
    Guid InviterId);
