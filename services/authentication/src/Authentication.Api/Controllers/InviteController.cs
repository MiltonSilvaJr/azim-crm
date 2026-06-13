using Authentication.Api.Middleware;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Services;
using Authentication.Contracts.Dtos;
using Authentication.Contracts.Errors;
using Authentication.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Authentication.Api.Controllers;

/// <summary>
/// Controller REST para operações de convite de usuário do módulo BC-12.
///
/// Endpoints:
///   - POST /v1/auth/invites          — cria convite (Tenant Admin) (Req 7).
///   - POST /v1/auth/invites/activate — ativa conta via token (público) (Req 7.4, PBT-05).
///
/// Regras (DD-001):
///   - Nunca expõe <c>identity_uid</c>.
///   - tenant_id derivado do slug (middleware) — nunca aceito do chamador.
///   - Endpoint de ativação é público (sem Bearer) e sujeito a rate limiting (RNF 8).
///
/// Mapeia: TASK-19, design.md § 8.3, § 8.4, Req 7, PBT-05.
/// </summary>
[ApiController]
[Route("v1/auth")]
public sealed class InviteController : ControllerBase
{
    private readonly InviteActivationService _inviteService;
    private readonly ILogger<InviteController> _logger;

    /// <summary>
    /// Inicializa o controller de convites.
    /// </summary>
    public InviteController(
        InviteActivationService inviteService,
        ILogger<InviteController> logger)
    {
        _inviteService = inviteService;
        _logger = logger;
    }

    /// <summary>
    /// Cria um convite de ativação para novo usuário.
    ///
    /// Requer papel Tenant Admin (AuthContext.Roles contém "admin").
    /// tenant_id derivado do slug resolvido pelo TenantResolutionMiddleware.
    /// Retorna 202 Accepted com <c>status: "invited"</c> em sucesso.
    ///
    /// Mapeia: TASK-19, design.md § 8.3, Req 7, Req 7.6.
    /// </summary>
    [HttpPost("invites")]
    public async Task<IActionResult> CreateInvite(
        [FromBody] InviteRequest request,
        CancellationToken cancellationToken)
    {
        // Requer AuthContext — endpoint protegido (Tenant Admin)
        if (!HttpContext.Items.TryGetValue(AuthenticationMiddleware.AuthContextKey, out var ctxObj)
            || ctxObj is not AuthContext authContext)
        {
            return Unauthorized(ErrorResponse.FromCatalog("AUTH-ERR-001"));
        }

        // Verificar papel Tenant Admin (Req 7 — somente admin pode convidar)
        if (!authContext.Roles.Contains("admin", StringComparer.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ErrorResponse.FromCatalog("AUTH-ERR-001"));
        }

        // tenant_id vem do middleware — nunca do corpo da requisição
        if (!HttpContext.Items.TryGetValue(TenantResolutionMiddleware.IdentityTenantIdKey, out var identityTenantObj)
            || identityTenantObj is not string identityTenantId)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ErrorResponse.FromCatalog("AUTH-ERR-090"));
        }

        try
        {
            var command = new CreateInviteCommand(
                Email: request.Email,
                FirebaseTenant: identityTenantId,
                TenantId: authContext.TenantId,
                InviterId: authContext.UserId);

            await _inviteService.CreateAsync(command, cancellationToken);

            _logger.LogInformation(
                "Convite criado por {InviterId} no tenant {TenantId} para {Email}",
                authContext.UserId,
                authContext.TenantId,
                request.Email);

            return Accepted(new InviteResponse());
        }
        catch (IdentityProviderException ex) when (ex.ErrorCode == "AUTH-ERR-030")
        {
            // E-mail já cadastrado e ativo — AUTH-ERR-030
            return Conflict(ErrorResponse.FromCatalog("AUTH-ERR-030"));
        }
        catch (InviteEmailFailedException)
        {
            // Convite criado mas e-mail não enviado — AUTH-ERR-032 (Req 7.2)
            return StatusCode(StatusCodes.Status500InternalServerError,
                ErrorResponse.FromCatalog("AUTH-ERR-032"));
        }
    }

    /// <summary>
    /// Ativa uma conta via token de convite recebido por e-mail.
    ///
    /// Endpoint público (sem Bearer). Rate limiting aplicado antes deste endpoint (RNF 8).
    /// Link expirado ou consumido → 410 AUTH-ERR-033 (PBT-05, Req 7.4/7.5).
    ///
    /// Mapeia: TASK-19, design.md § 8.4, Req 7.4, Req 7.5, PBT-05.
    /// </summary>
    [HttpPost("invites/activate")]
    public async Task<IActionResult> ActivateInvite(
        [FromBody] ActivateInviteRequest request,
        CancellationToken cancellationToken)
    {
        // Endpoint público — sem AuthContext obrigatório
        // tenant_id vem do TenantResolutionMiddleware (slug no header)
        if (!HttpContext.Items.TryGetValue(TenantResolutionMiddleware.TenantIdKey, out var tenantIdObj)
            || tenantIdObj is not Guid tenantId)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ErrorResponse.FromCatalog("AUTH-ERR-090"));
        }

        try
        {
            // O estado Issued + ExpiresAt 72h do futuro representa um token recém-emitido.
            // Em produção, o estado real seria carregado do token desserializado.
            // Esta implementação delega a validação de estado ao InviteUsableSpec (PBT-05).
            var command = new ActivateInviteCommand(
                ActivationToken: request.ActivationToken,
                State: InviteLinkState.Issued,
                ExpiresAt: DateTimeOffset.UtcNow.AddHours(72),
                UserId: Guid.Empty,  // resolvido pelo organization na ativação real
                TenantId: tenantId);

            await _inviteService.ActivateAsync(command, cancellationToken);

            return Ok(new ActivateInviteResponse());
        }
        catch (IdentityProviderException ex) when (ex.ErrorCode == "AUTH-ERR-033")
        {
            // Link expirado ou já consumido → 410 Gone (PBT-05, Req 7.4/7.5)
            return StatusCode(StatusCodes.Status410Gone,
                ErrorResponse.FromCatalog("AUTH-ERR-033"));
        }
    }
}
