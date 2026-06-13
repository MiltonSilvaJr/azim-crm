namespace Authentication.Application.Services;

/// <summary>
/// Comando para solicitar redefinição de senha.
///
/// Handler: <see cref="PasswordResetService.RequestAsync"/>.
///
/// Resposta sempre idêntica (accepted) independente de o e-mail existir ou não
/// (anti-enumeração — PBT-03, Req 8.3, Req 10.2).
///
/// Mapeia: design.md § 5.1, Req 8, PBT-03.
/// </summary>
/// <param name="Email">E-mail que solicitou a redefinição.</param>
/// <param name="FirebaseTenant">Tenant de identidade do contexto da requisição.</param>
/// <param name="TenantId">UUID do tenant Azim.</param>
public sealed record RequestPasswordResetCommand(
    string Email,
    string FirebaseTenant,
    Guid TenantId);
