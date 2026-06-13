using Authentication.Domain.ValueObjects;

namespace Authentication.Application.Services;

/// <summary>
/// Comando para ativar conta via link de convite.
///
/// Handler: <see cref="InviteActivationService.ActivateAsync"/>.
///
/// Aplica <c>InviteUsableSpec</c>: link expirado ou consumido → AUTH-ERR-033 (410).
/// Link usado com sucesso → evento auditável <c>invite_activated</c>.
///
/// Mapeia: design.md § 5.1, Req 7.4, 7.5, PBT-05, DD-009.
/// </summary>
/// <param name="ActivationToken">Token de ativação recebido do link.</param>
/// <param name="State">Estado atual do link (Issued, Consumed ou Expired).</param>
/// <param name="ExpiresAt">Momento UTC de expiração do link (DD-009: 72h).</param>
/// <param name="UserId">UUID do usuário que está ativando (no módulo organization).</param>
/// <param name="TenantId">UUID do tenant.</param>
public sealed record ActivateInviteCommand(
    string ActivationToken,
    InviteLinkState State,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    Guid TenantId);
