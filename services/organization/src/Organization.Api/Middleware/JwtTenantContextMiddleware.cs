using Organization.Application.Ports;
using System.Security.Claims;

namespace Organization.Api.Middleware;

/// <summary>
/// Middleware que extrai claims do JWT e popula o <see cref="ITenantContext"/> para o request.
/// Claims esperadas: <c>tenant_id</c>, <c>sub</c> (user_id), <c>roles</c>, <c>bu_roles</c>.
/// Registra como serviço scoped (<see cref="JwtTenantContext"/>).
/// </summary>
public sealed class JwtTenantContextMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Inicializa o middleware.</summary>
    public JwtTenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context, JwtTenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // tenant_id
            var tenantIdStr = context.User.FindFirstValue("tenant_id");
            if (Guid.TryParse(tenantIdStr, out var tenantId))
                tenantContext.TenantId = tenantId;

            // user_id / sub
            var userIdStr = context.User.FindFirstValue("sub")
                ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(userIdStr, out var userId))
                tenantContext.UserId = userId;

            // correlation_id — propagado via header X-Correlation-Id (ADR-0009)
            var correlationHeader = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
            tenantContext.CorrelationId = Guid.TryParse(correlationHeader, out var corrId)
                ? corrId
                : Guid.NewGuid();

            // bu_roles: claims no formato "bu_id:role" (ex.: "3fa85f64-...:TAdmin")
            var buRoles = new Dictionary<Guid, string>();
            foreach (var claim in context.User.FindAll("bu_role"))
            {
                var parts = claim.Value.Split(':', 2);
                if (parts.Length == 2 && Guid.TryParse(parts[0], out var buId))
                    buRoles[buId] = parts[1];
            }

            tenantContext.RolesByBu = buRoles;
        }

        await _next(context);
    }
}

/// <summary>
/// Implementação de <see cref="ITenantContext"/> populada pelo <see cref="JwtTenantContextMiddleware"/>.
/// Registrada como scoped — uma instância por request HTTP.
/// </summary>
public sealed class JwtTenantContext : ITenantContext
{
    /// <inheritdoc/>
    public Guid TenantId { get; set; }

    /// <inheritdoc/>
    public Guid UserId { get; set; }

    /// <inheritdoc/>
    public Guid CorrelationId { get; set; } = Guid.NewGuid();

    /// <inheritdoc/>
    public IReadOnlyDictionary<Guid, string> RolesByBu { get; set; }
        = new Dictionary<Guid, string>();
}

/// <summary>Extensões de registro do contexto JWT no pipeline.</summary>
public static class JwtTenantContextExtensions
{
    /// <summary>Registra o <see cref="JwtTenantContext"/> como <see cref="ITenantContext"/> scoped.</summary>
    public static IServiceCollection AddJwtTenantContext(this IServiceCollection services)
    {
        services.AddScoped<JwtTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<JwtTenantContext>());
        return services;
    }

    /// <summary>Adiciona o <see cref="JwtTenantContextMiddleware"/> ao pipeline.</summary>
    public static IApplicationBuilder UseJwtTenantContext(this IApplicationBuilder app)
    {
        app.UseMiddleware<JwtTenantContextMiddleware>();
        return app;
    }
}
