using AuditLog.Application.Abstractions;

namespace AuditLog.Api.Infrastructure;

/// <summary>
/// Implementação de <see cref="IUserContext"/> baseada no token JWT corrente.
/// Lê o claim <see cref="System.Security.Claims.ClaimTypes.Role"/> do contexto HTTP.
/// </summary>
internal sealed class HttpUserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpUserContext(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public string? Role
    {
        get
        {
            var ctx = _httpContextAccessor.HttpContext;
            return ctx?.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        }
    }
}
