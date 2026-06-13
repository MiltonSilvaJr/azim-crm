using Authentication.Api.Middleware;
using Authentication.Application.Services;
using Authentication.Contracts.Dtos;
using Authentication.Contracts.Errors;
using Authentication.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Authentication.Api.Controllers;

/// <summary>
/// Controller REST para operações de autenticação do módulo BC-12.
///
/// Endpoints:
///   - GET  /v1/auth/me     — retorna AuthContext do usuário autenticado (Req 11).
///   - POST /v1/auth/logout — revoga sessão global idempotente (Req 9, PBT-04).
///
/// Regras (DD-001):
///   - Nunca expõe <c>identity_uid</c>.
///   - Nunca referencia tipos do Firebase Admin SDK.
///   - Delega toda lógica de negócio para a camada Application.
///
/// Mapeia: TASK-18, design.md § 8.1, § 8.2, Req 9, Req 11.
/// </summary>
[ApiController]
[Route("v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly SessionRevocationService _revocationService;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Inicializa o controller de autenticação.
    /// </summary>
    public AuthController(
        SessionRevocationService revocationService,
        ILogger<AuthController> logger)
    {
        _revocationService = revocationService;
        _logger = logger;
    }

    /// <summary>
    /// Retorna o contexto de autenticação do usuário corrente.
    ///
    /// Requer token válido (AuthContext injetado pelo <see cref="AuthenticationMiddleware"/>).
    /// Sem token → 401 AUTH-ERR-001 (Req 4.2).
    /// Token inválido → 401 (tratado pelo middleware antes deste endpoint).
    ///
    /// Mapeia: TASK-18, design.md § 8.1, Req 11, Req 11.4.
    /// </summary>
    [HttpGet("me")]
    public IActionResult GetMe()
    {
        // AuthContext injetado pelo AuthenticationMiddleware — ausente = sem token (Req 4.2)
        if (!HttpContext.Items.TryGetValue(AuthenticationMiddleware.AuthContextKey, out var ctxObj)
            || ctxObj is not AuthContext authContext)
        {
            return Unauthorized(ErrorResponse.FromCatalog("AUTH-ERR-001"));
        }

        var response = new MeResponse
        {
            UserId = authContext.UserId,
            Email = authContext.Email,
            TenantId = authContext.TenantId,
            Roles = authContext.Roles,
            Memberships = authContext.Memberships.Entries
                .Select(e => new Contracts.Dtos.MembershipEntry
                {
                    BuId = e.BuId,
                    Role = e.Role
                })
                .ToList()
        };

        return Ok(response);
    }

    /// <summary>
    /// Revoga a sessão global do usuário corrente (logout).
    ///
    /// Idempotente: N chamadas consecutivas retornam 200 com <c>status: "revoked"</c>
    /// sem erro (Req 9.5, PBT-04).
    ///
    /// Requer token válido. Sem autenticação → 401.
    ///
    /// Mapeia: TASK-18, design.md § 8.2, Req 9, PBT-04.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        // AuthContext injetado pelo middleware — ausente = sem token
        if (!HttpContext.Items.TryGetValue(AuthenticationMiddleware.AuthContextKey, out var ctxObj)
            || ctxObj is not AuthContext authContext)
        {
            return Unauthorized(ErrorResponse.FromCatalog("AUTH-ERR-001"));
        }

        var command = new LogoutCommand(
            UserId: authContext.UserId,
            TenantId: authContext.TenantId);

        await _revocationService.HandleAsync(command, cancellationToken);

        _logger.LogInformation(
            "Sessão revogada para usuário {UserId} no tenant {TenantId}",
            authContext.UserId,
            authContext.TenantId);

        return Ok(new LogoutResponse());
    }
}
