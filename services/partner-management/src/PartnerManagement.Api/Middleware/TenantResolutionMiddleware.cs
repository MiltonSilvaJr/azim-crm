using System.Security.Claims;

namespace PartnerManagement.Api.Middleware;

/// <summary>
/// Middleware que resolve o <c>tenant_id</c> do token JWT e popula o <c>TenantContext</c>.
/// Lê o claim <c>tenant_id</c> do principal autenticado e define o tenant corrente.
/// Em testes, o <c>TenantContext</c> concreto pode não existir (substituído por mock).
/// Requisições não autenticadas passam adiante (o ASP.NET Authorization cuida do 401).
/// Mapeia: TASK-23, RNF 1, DD-001, design §5.4.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string TenantIdClaim = "tenant_id";

    /// <inheritdoc cref="IMiddleware.InvokeAsync"/>
    public async Task InvokeAsync(HttpContext context, IServiceProvider services)
    {
        // Endpoints públicos (health, metrics) não exigem tenant
        if (!(context.User.Identity?.IsAuthenticated ?? false))
        {
            await next(context);
            return;
        }

        string? tenantIdValue = context.User.FindFirstValue(TenantIdClaim);

        if (!string.IsNullOrEmpty(tenantIdValue)
            && Guid.TryParse(tenantIdValue, out Guid tenantId)
            && tenantId != Guid.Empty)
        {
            // Tentar setar o TenantContext concreto se disponível
            // Em testes, o TenantContext concreto pode não existir — o mock de ITenantContext é usado
            PartnerManagement.Infrastructure.Tenancy.TenantContext? tenantContext =
                services.GetService<PartnerManagement.Infrastructure.Tenancy.TenantContext>();

            tenantContext?.SetTenant(tenantId);
        }

        await next(context);
    }
}
