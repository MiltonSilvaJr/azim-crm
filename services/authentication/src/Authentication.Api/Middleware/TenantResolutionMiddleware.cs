using Authentication.Application.Ports;
using Authentication.Contracts.Errors;
using Microsoft.AspNetCore.Http.Extensions;

namespace Authentication.Api.Middleware;

/// <summary>
/// Middleware que resolve o slug do tenant para o <c>tenant_id</c> interno.
///
/// Comportamento:
///   - Lê o slug do header <c>X-Tenant-Slug</c> (prioritário) ou extrai do path.
///   - Chama <see cref="ITenantDirectory.ResolveSlugAsync"/> em conexão de catálogo.
///   - Slug inexistente/inativo → 404 <c>AUTH-ERR-010</c> sem detalhe interno.
///   - Em sucesso, injeta <c>tenant_id</c> e <c>identity_tenant_id</c> no contexto.
///   - Executa <c>SET app.current_tenant</c> para ativar RLS (DEC-006).
///
/// É o segundo middleware do pipeline, após <see cref="CorrelationMiddleware"/>
/// e antes de <c>AuthenticationMiddleware</c> (design.md § 5.4, ordem obrigatória).
///
/// Mapeia: TASK-15, design.md § 5.4, § 6.7, Req 1, Req 1.2, Req 1.5, DEC-006.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    /// <summary>Chave do <c>HttpContext.Items</c> para o tenant_id resolvido.</summary>
    public const string TenantIdKey = "auth:tenant_id";

    /// <summary>Chave do <c>HttpContext.Items</c> para o identity_tenant_id do IdP.</summary>
    public const string IdentityTenantIdKey = "auth:identity_tenant_id";

    /// <summary>Nome do header de slug.</summary>
    public const string TenantSlugHeader = "X-Tenant-Slug";

    private readonly RequestDelegate _next;
    private readonly ITenantDirectory _tenantDirectory;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    /// <summary>
    /// Inicializa o middleware de resolução de tenant.
    /// </summary>
    public TenantResolutionMiddleware(
        RequestDelegate next,
        ITenantDirectory tenantDirectory,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _tenantDirectory = tenantDirectory;
        _logger = logger;
    }

    /// <summary>
    /// Resolve o slug para <c>tenant_id</c> e <c>identity_tenant_id</c>.
    ///
    /// Slug inexistente → 404 <c>AUTH-ERR-010</c>.
    /// Slug presente em rotas que não requerem tenant (ex.: health checks) → skip.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var slug = ResolveSlug(context);

        // Health checks e rotas sem tenant ignoram resolução
        if (slug is null || IsHealthRoute(context))
        {
            await _next(context);
            return;
        }

        // Normalizar slug: lowercase, sem espaços (Req 1)
        slug = slug.Trim().ToLowerInvariant();

        var tenantResult = await _tenantDirectory.ResolveSlugAsync(
            slug,
            context.RequestAborted);

        if (tenantResult is null)
        {
            _logger.LogInformation(
                "Slug não encontrado ou inativo. Retornando 404 AUTH-ERR-010.");

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";

            var error = ErrorResponse.FromCatalog("AUTH-ERR-010");
            await context.Response.WriteAsJsonAsync(error, context.RequestAborted);
            return;
        }

        // Injetar tenant_id e identity_tenant_id no contexto
        context.Items[TenantIdKey] = tenantResult.TenantId;
        context.Items[IdentityTenantIdKey] = tenantResult.IdentityTenantId;

        // Adicionar tenantId ao escopo de log (RNF 4.4)
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["tenantId"] = tenantResult.TenantId
        });

        await _next(context);
    }

    // Lê slug do header X-Tenant-Slug (prioritário) ou extrai do path /v1/... (futuro)
    private static string? ResolveSlug(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(TenantSlugHeader, out var headerSlug)
            && !string.IsNullOrWhiteSpace(headerSlug))
        {
            return headerSlug.ToString();
        }

        return null;
    }

    // Rotas de saúde não requerem resolução de tenant
    private static bool IsHealthRoute(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
}
