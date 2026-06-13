using Authentication.Application.Ports;
using Authentication.Contracts.Errors;

namespace Authentication.Api.Middleware;

/// <summary>
/// Middleware de rate limiting por IP e por tenant.
///
/// Limita requisições usando <see cref="IRateLimiter"/> (janela deslizante por chave).
/// Excesso → 429 <c>AUTH-ERR-040</c> com mensagem genérica que não revela
/// existência de conta (RNF 8.2, design.md § 5.4).
///
/// Precede <see cref="AuthenticationMiddleware"/> para evitar oráculo de timing
/// em fluxos públicos (Req 10.5, design.md § 5.4).
///
/// Mapeia: TASK-16, design.md § 5.4, RNF 8, Req 10.5.
/// </summary>
public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimiter _rateLimiter;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    /// <summary>
    /// Inicializa o middleware de rate limiting.
    /// </summary>
    public RateLimitingMiddleware(
        RequestDelegate next,
        IRateLimiter rateLimiter,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    /// <summary>
    /// Verifica o limite de taxa antes de prosseguir no pipeline.
    /// Excesso → 429 sem revelar motivo que indique conta (RNF 8.2).
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var partitionKey = BuildPartitionKey(context);

        var isAllowed = await _rateLimiter.IsAllowedAsync(
            partitionKey,
            context.RequestAborted);

        if (!isAllowed)
        {
            _logger.LogWarning(
                "Rate limit excedido. PartitionKey: {PartitionKey}",
                partitionKey);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";

            var error = ErrorResponse.FromCatalog("AUTH-ERR-040");
            await context.Response.WriteAsJsonAsync(error, context.RequestAborted);
            return;
        }

        await _next(context);
    }

    // Constrói chave de particionamento composta por IP e tenant_id (quando disponível)
    private static string BuildPartitionKey(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (context.Items.TryGetValue(TenantResolutionMiddleware.TenantIdKey, out var tenantId)
            && tenantId is Guid tenantGuid)
        {
            return $"tenant:{tenantGuid}:{ip}";
        }

        return $"ip:{ip}";
    }
}
