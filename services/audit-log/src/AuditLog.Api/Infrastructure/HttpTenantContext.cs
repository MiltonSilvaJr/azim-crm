using AuditLog.Application.Abstractions;

namespace AuditLog.Api.Infrastructure;

/// <summary>
/// Implementação de <see cref="ITenantContext"/> baseada no token JWT corrente.
/// Lê o claim <c>tenant_id</c> do <see cref="IHttpContextAccessor"/> para derivar o tenant
/// sem aceitar o valor do chamador externo (REQ-005.3).
/// </summary>
internal sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public Guid? TenantId
    {
        get
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx is null) return null;

            var claim = ctx.User?.FindFirst("tenant_id")?.Value;
            if (claim is not null && Guid.TryParse(claim, out var id))
                return id;

            return null;
        }
    }
}
