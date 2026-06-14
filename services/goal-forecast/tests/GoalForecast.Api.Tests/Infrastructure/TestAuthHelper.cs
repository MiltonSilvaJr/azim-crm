namespace GoalForecast.Api.Tests.Infrastructure;

/// <summary>
/// Auxiliar para construção de headers de autenticação de teste.
/// Mapeia: TASK-22..25, design §10.
/// </summary>
public static class TestAuthHelper
{
    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    public static readonly Guid BuA1 = Guid.Parse("aaaaaaaa-0001-0000-0000-000000000001");
    public static readonly Guid BuA2 = Guid.Parse("aaaaaaaa-0002-0000-0000-000000000001");
    public static readonly Guid UserId1 = Guid.Parse("cccccccc-0001-0000-0000-000000000001");
    public static readonly Guid UserId2 = Guid.Parse("cccccccc-0002-0000-0000-000000000001");

    /// <summary>
    /// Header de claims para TenantAdmin no tenant A.
    /// </summary>
    public static string TenantAdminA =>
        $"role=TenantAdmin,tenantId={TenantA},userId={UserId1}";

    /// <summary>
    /// Header de claims para GestorDeBu na BU A1 do tenant A.
    /// </summary>
    public static string GestorBuA1 =>
        $"role=GestorDeBu,tenantId={TenantA},userId={UserId1},buId={BuA1}";

    /// <summary>
    /// Header de claims para Executivo no tenant A.
    /// </summary>
    public static string ExecutivoA =>
        $"role=Executivo,tenantId={TenantA},userId={UserId1}";

    /// <summary>
    /// Header de claims para Vendedor no tenant A (UserId1 é o próprio owner).
    /// </summary>
    public static string VendedorA =>
        $"role=Vendedor,tenantId={TenantA},userId={UserId1}";

    /// <summary>
    /// Header de claims para serviço interno (Digest worker).
    /// </summary>
    public static string ServiceScope =>
        $"role=ServiceScope,tenantId={TenantA},userId={UserId1}";
}
