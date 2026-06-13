using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Services;
using Authentication.Contracts.Errors;
using Authentication.Domain.ValueObjects;

namespace Authentication.Api.Middleware;

/// <summary>
/// Middleware de autenticação: valida o token JWT e injeta o <see cref="AuthContext"/>.
///
/// Fluxo (design.md § 5.4):
///   1. Lê <c>Authorization: Bearer {jwt}</c> do header.
///   2. Token ausente → prossegue sem AuthContext (para rotas públicas não requer auth).
///   3. Token presente → invoca <see cref="SessionTokenValidator"/> (assinatura + tenant).
///   4. Em sucesso → compõe <see cref="AuthContext"/> via <see cref="AuthContextComposer"/>.
///   5. Injeta AuthContext em <c>HttpContext.Items</c>.
///   6. Falha de token → 401 (AUTH-ERR-001/002/003/004).
///   7. Usuário inativo → 403 (AUTH-ERR-005).
///
/// Mapeia: TASK-16, design.md § 5.4, Req 4, Req 5.4, PBT-02.
/// </summary>
public sealed class AuthenticationMiddleware
{
    /// <summary>Chave do <c>HttpContext.Items</c> para o <see cref="AuthContext"/>.</summary>
    public const string AuthContextKey = "auth:context";

    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationMiddleware> _logger;

    /// <summary>
    /// Inicializa o middleware de autenticação.
    /// </summary>
    public AuthenticationMiddleware(
        RequestDelegate next,
        ILogger<AuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Valida o token e injeta <see cref="AuthContext"/> quando presente.
    ///
    /// Sem token → prossegue (rotas públicas tratam ausência de AuthContext).
    /// Token inválido → 401 sem detalhe interno.
    /// Usuário inativo → 403 sem detalhe interno.
    /// </summary>
    public async Task InvokeAsync(
        HttpContext context,
        SessionTokenValidator tokenValidator,
        AuthContextComposer contextComposer)
    {
        var rawJwt = ExtractBearerToken(context);

        // Sem token — prossegue para rotas públicas
        if (rawJwt is null)
        {
            await _next(context);
            return;
        }

        // Resolver tenant_id e identity_tenant_id (injetados pelo TenantResolutionMiddleware)
        if (!context.Items.TryGetValue(TenantResolutionMiddleware.TenantIdKey, out var tenantIdObj)
            || tenantIdObj is not Guid tenantId
            || !context.Items.TryGetValue(TenantResolutionMiddleware.IdentityTenantIdKey, out var identityTenantObj)
            || identityTenantObj is not string identityTenantId)
        {
            // Tenant não foi resolvido — rotas que exigem auth sem tenant falham com 401
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "AUTH-ERR-001");
            return;
        }

        try
        {
            // Valida token (assinatura, lifetime, tenant — PBT-02)
            var (_, verifyResult) = await tokenValidator.ValidateAsync(
                rawJwt,
                identityTenantId,
                tenantId,
                context.RequestAborted);

            // Compõe AuthContext (resolve user_id + memberships, aplica ActiveUserSpec)
            var authContext = await contextComposer.ComposeAsync(
                verifyResult,
                tenantId,
                context.RequestAborted);

            // Injeta AuthContext para consumo pelos controllers
            context.Items[AuthContextKey] = authContext;

            await _next(context);
        }
        catch (IdentityProviderException ex)
        {
            _logger.LogInformation(
                "Falha de autenticação. Código: {ErrorCode}",
                ex.ErrorCode);

            var (statusCode, errorCode) = MapToHttpStatus(ex.ErrorCode);
            await WriteErrorAsync(context, statusCode, errorCode);
        }
    }

    // Extrai token JWT do header Authorization: Bearer {token}
    private static string? ExtractBearerToken(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
            return null;

        var header = authHeader.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = header["Bearer ".Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    // Mapeia código de erro interno para status HTTP + código do catálogo
    private static (int StatusCode, string ErrorCode) MapToHttpStatus(string errorCode) =>
        errorCode switch
        {
            "AUTH-ERR-001" => (StatusCodes.Status401Unauthorized, "AUTH-ERR-001"),
            "AUTH-ERR-002" => (StatusCodes.Status401Unauthorized, "AUTH-ERR-002"),
            "AUTH-ERR-003" => (StatusCodes.Status401Unauthorized, "AUTH-ERR-003"),
            "AUTH-ERR-004" => (StatusCodes.Status401Unauthorized, "AUTH-ERR-004"),
            "AUTH-ERR-005" => (StatusCodes.Status403Forbidden, "AUTH-ERR-005"),
            "AUTH-ERR-020" => (StatusCodes.Status503ServiceUnavailable, "AUTH-ERR-020"),
            _ => (StatusCodes.Status401Unauthorized, "AUTH-ERR-001")
        };

    // Escreve resposta de erro no formato { code, message } sem detalhe interno
    private static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string errorCode)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var error = ErrorResponse.FromCatalog(errorCode);
        await context.Response.WriteAsJsonAsync(error, context.RequestAborted);
    }
}
