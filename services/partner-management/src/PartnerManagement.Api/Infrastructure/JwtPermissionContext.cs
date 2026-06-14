using PartnerManagement.Application.Ports;

namespace PartnerManagement.Api.Infrastructure;

/// <summary>
/// Implementação de <see cref="IPermissionContext"/> que lê o claim <c>permissions</c> do JWT.
/// Registrada como Scoped no DI da Api (um por requisição HTTP).
/// Mapeia: RNF 1, design §10, rule jwt-permissions.md, TASK-23.
/// </summary>
public sealed class JwtPermissionContext(IHttpContextAccessor httpContextAccessor) : IPermissionContext
{
    private const string PermissionsClaim = "permissions";

    /// <inheritdoc/>
    public IReadOnlyList<string> Permissions { get; } = BuildPermissions(httpContextAccessor);

    /// <inheritdoc/>
    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> BuildPermissions(IHttpContextAccessor accessor)
    {
        IEnumerable<System.Security.Claims.Claim>? claims = accessor.HttpContext?.User?.Claims;
        if (claims is null)
        {
            return [];
        }

        return claims
            .Where(c => c.Type == PermissionsClaim)
            .Select(c => c.Value)
            .ToList()
            .AsReadOnly();
    }
}
