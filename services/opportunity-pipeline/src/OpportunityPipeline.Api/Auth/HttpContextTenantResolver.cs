using System.Security.Claims;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;

namespace OpportunityPipeline.Api.Auth;

/// <summary>
/// Implementação de ITenantResolver para contexto HTTP.
/// Extrai tenant_id, bu_id, actor_id e papel do JWT (claims do usuário autenticado).
/// Mapeia: design §5.4, ADR-0001, TASK-20.
/// </summary>
public sealed class HttpContextTenantResolver(IHttpContextAccessor httpContextAccessor)
    : ITenantResolver
{
    public Task<(Guid TenantId, Guid BuId, Guid ActorId, UserRole Role)> ResolveAsync(
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext não disponível.");

        var user = httpContext.User;

        var tenantIdStr = user.FindFirstValue("tenant_id")
            ?? throw new UnauthorizedAccessException("Claim 'tenant_id' ausente no JWT.");

        var buIdStr = user.FindFirstValue("bu_id")
            ?? throw new UnauthorizedAccessException("Claim 'bu_id' ausente no JWT.");

        var actorIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("Claim de identidade (sub/NameIdentifier) ausente no JWT.");

        var roleStr = user.FindFirstValue("role")
            ?? user.FindFirstValue(ClaimTypes.Role)
            ?? nameof(UserRole.Viewer);

        if (!Guid.TryParse(tenantIdStr, out var tenantId))
            throw new UnauthorizedAccessException($"Claim 'tenant_id' inválido: '{tenantIdStr}'.");

        if (!Guid.TryParse(buIdStr, out var buId))
            throw new UnauthorizedAccessException($"Claim 'bu_id' inválido: '{buIdStr}'.");

        if (!Guid.TryParse(actorIdStr, out var actorId))
            throw new UnauthorizedAccessException($"Claim de identidade inválido: '{actorIdStr}'.");

        if (!Enum.TryParse<UserRole>(roleStr, ignoreCase: true, out var role))
            role = UserRole.Viewer;

        return Task.FromResult((tenantId, buId, actorId, role));
    }
}
