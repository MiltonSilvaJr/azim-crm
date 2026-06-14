namespace ActivityManagement.Api.Tests.Helpers;

using System.Security.Claims;

/// <summary>
/// Auxiliar para criação de claims de teste por papel e tenant.
/// Usado pelos testes de RBAC e cross-tenant (TASK-21).
/// </summary>
public static class TestJwtHelper
{
    /// <summary>Cria claims para um seller do tenant informado.</summary>
    public static IEnumerable<Claim> SellerClaims(Guid tenantId, Guid? userId = null, Guid? buId = null) =>
        BuildClaims("seller", tenantId, userId ?? Guid.NewGuid(), buId ?? Guid.NewGuid());

    /// <summary>Cria claims para um bu_manager do tenant informado.</summary>
    public static IEnumerable<Claim> BuManagerClaims(Guid tenantId, Guid? userId = null, Guid? buId = null) =>
        BuildClaims("bu_manager", tenantId, userId ?? Guid.NewGuid(), buId ?? Guid.NewGuid());

    /// <summary>Cria claims para um viewer do tenant informado.</summary>
    public static IEnumerable<Claim> ViewerClaims(Guid tenantId, Guid? userId = null, Guid? buId = null) =>
        BuildClaims("viewer", tenantId, userId ?? Guid.NewGuid(), buId ?? Guid.NewGuid());

    private static IEnumerable<Claim> BuildClaims(string role, Guid tenantId, Guid userId, Guid buId) =>
    [
        new Claim("sub",       userId.ToString()),
        new Claim("tenant_id", tenantId.ToString()),
        new Claim("bu_id",     buId.ToString()),
        new Claim("role",      role),
    ];
}
