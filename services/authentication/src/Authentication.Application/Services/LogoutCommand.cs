namespace Authentication.Application.Services;

/// <summary>
/// Comando para revogar a sessão global do usuário corrente.
///
/// Handler: <see cref="SessionRevocationService"/>.
///
/// A revogação é global: invalida todos os refresh tokens do usuário no IdP
/// (Req 9.1). A operação é idempotente (PBT-04, Req 9.5).
///
/// Mapeia: design.md § 5.1, Req 9.
/// </summary>
/// <param name="UserId">UUID interno do usuário no tenant (nunca identity_uid — DD-001).</param>
/// <param name="TenantId">UUID do tenant ao qual o usuário pertence.</param>
public sealed record LogoutCommand(Guid UserId, Guid TenantId);
