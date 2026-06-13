using System.Security.Claims;
using TenantAdministration.Application.Ports;

namespace TenantAdministration.Api.Infrastructure;

/// <summary>
/// Implementação de <see cref="ICurrentUserContext"/> baseada no <see cref="HttpContext"/> corrente.
/// Extrai o papel do usuário dos claims JWT ou do header de teste (ambiente de testes).
/// Nunca expõe dados sensíveis (PII) nos logs.
/// </summary>
public sealed class HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserContext
{
    private ClaimsPrincipal? Principal =>
        httpContextAccessor.HttpContext?.User;

    /// <inheritdoc/>
    public string UserId =>
        Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Principal?.FindFirstValue("sub")
        ?? string.Empty;

    /// <inheritdoc/>
    public bool IsPlatformOperator =>
        HasRole("PlatformOperator");

    /// <inheritdoc/>
    public bool IsTenantAdmin =>
        HasRole("TenantAdmin");

    /// <inheritdoc/>
    public bool IsViewer =>
        HasRole("Viewer") || IsTenantAdmin;

    private bool HasRole(string role) =>
        Principal?.Claims.Any(c =>
            c.Type == "role" && c.Value == role) ?? false;
}
