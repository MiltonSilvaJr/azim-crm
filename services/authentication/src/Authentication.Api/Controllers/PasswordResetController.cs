using Authentication.Api.Middleware;
using Authentication.Application.Services;
using Authentication.Contracts.Dtos;
using Authentication.Contracts.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Authentication.Api.Controllers;

/// <summary>
/// Controller REST para solicitação de redefinição de senha.
///
/// Endpoint:
///   - POST /v1/auth/password-reset — endpoint público; sempre retorna 202 (PBT-03).
///
/// Anti-enumeração (Req 8.3, Req 10.2, PBT-03):
///   - Corpo da resposta idêntico para e-mail existente e inexistente.
///   - Código HTTP sempre 202 (Accepted).
///   - Timing equalizado pelo delay constante no <see cref="PasswordResetService"/>.
///   - Nenhum detalhe interno (existência de conta, IdP, SQL) exposto.
///
/// Regras (DD-001):
///   - Nunca expõe <c>identity_uid</c>.
///   - tenant_id derivado do slug (middleware).
///
/// Mapeia: TASK-20, design.md § 8.5, Req 8, Req 8.3, Req 10.2, PBT-03, RISK-AUTH-05.
/// </summary>
[ApiController]
[Route("v1/auth")]
public sealed class PasswordResetController : ControllerBase
{
    private readonly PasswordResetService _resetService;
    private readonly ILogger<PasswordResetController> _logger;

    /// <summary>
    /// Inicializa o controller de redefinição de senha.
    /// </summary>
    public PasswordResetController(
        PasswordResetService resetService,
        ILogger<PasswordResetController> logger)
    {
        _resetService = resetService;
        _logger = logger;
    }

    /// <summary>
    /// Solicita redefinição de senha.
    ///
    /// Anti-enumeração: sempre retorna 202 com <c>status: "accepted"</c>
    /// independente de o e-mail existir, ser inativo, ou pertencer a usuário Google.
    /// O delay constante do <see cref="PasswordResetService"/> equaliza o timing (PBT-03).
    ///
    /// Endpoint público — sem Bearer token.
    ///
    /// Mapeia: TASK-20, design.md § 8.5, Req 8, Req 8.3, Req 10.2, PBT-03.
    /// </summary>
    [HttpPost("password-reset")]
    public async Task<IActionResult> RequestPasswordReset(
        [FromBody] PasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        // tenant_id derivado do slug resolvido pelo TenantResolutionMiddleware
        if (!HttpContext.Items.TryGetValue(TenantResolutionMiddleware.TenantIdKey, out var tenantIdObj)
            || tenantIdObj is not Guid tenantId
            || !HttpContext.Items.TryGetValue(TenantResolutionMiddleware.IdentityTenantIdKey, out var identityTenantObj)
            || identityTenantObj is not string identityTenantId)
        {
            // Slug inválido já foi tratado pelo TenantResolutionMiddleware (404)
            // Este caminho só ocorre se o middleware falhou — erro interno
            return StatusCode(StatusCodes.Status500InternalServerError,
                ErrorResponse.FromCatalog("AUTH-ERR-090"));
        }

        var command = new RequestPasswordResetCommand(
            Email: request.Email,
            FirebaseTenant: identityTenantId,
            TenantId: tenantId);

        // Processa internamente — nunca propaga exceção ao caller (anti-enumeração, PBT-03)
        await _resetService.RequestAsync(command, cancellationToken);

        // Anti-enumeração: 202 uniforme independente do resultado interno (Req 8.3, PBT-03)
        return Accepted(new PasswordResetResponse());
    }
}
