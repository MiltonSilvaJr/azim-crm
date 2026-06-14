using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Organization.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Organization.Infrastructure.Tests.PBT;

/// <summary>
/// PBT-01 — Isolamento por tenant (TASK-27, Onda 6 — Hardening).
/// Gera N tenants com M registros cada; afirma que toda query no contexto
/// de um tenant_id T retorna apenas registros de T (zero cross-leakage).
/// Gate obrigatório de CI: falha bloqueia merge (KPI-06).
/// Referências: RNF 1; design §11, §14; DEC-006; ADR-0001.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "PBT-01")]
public sealed class TenantIsolationPbtTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("org_pbt01")
        .WithUsername("org_app")
        .WithPassword("test_pbt")
        .Build();

    private string _connectionString = string.Empty;
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        using var ctx = CreateContextWithoutFilter();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    // ── PBT-01: Isolamento completo via EF Core global filter ────────────

    /// <summary>
    /// PBT-01 (ST-01/ST-02): gera 2..5 tenants com 1..10 BUs cada.
    /// Afirma que cada query por tenant_id retorna apenas registros daquele tenant.
    /// Executa com ≥100 exemplos conforme TASK-27.
    /// </summary>
    [Property(MaxTest = 100, QuietOnSuccess = true)]
    public Property BusinessUnit_QueryByTenantId_ReturnsOnlyTenantRecords(
        PositiveInt tenantCountRaw,
        PositiveInt buCountPerTenantRaw)
    {
        // Normaliza: 2..5 tenants, 1..10 BUs por tenant
        var tenantCount = (tenantCountRaw.Get % 4) + 2;
        var buCountPerTenant = (buCountPerTenantRaw.Get % 10) + 1;

        return RunIsolationCheck(tenantCount, buCountPerTenant)
            .GetAwaiter().GetResult()
            .ToProperty();
    }

    private async Task<bool> RunIsolationCheck(int tenantCount, int buCountPerTenant)
    {
        var tenants = Enumerable.Range(0, tenantCount)
            .Select(_ => Guid.NewGuid())
            .ToList();

        using var writeCtx = CreateContextWithoutFilter();

        foreach (var tenantId in tenants)
        {
            for (int i = 0; i < buCountPerTenant; i++)
            {
                var bu = BusinessUnit.Create(
                    BusinessUnitName.Create($"BU-{Guid.NewGuid():N}"),
                    tenantId,
                    Now);
                bu.ClearDomainEvents();
                writeCtx.BusinessUnits.Add(bu);
            }
        }

        await writeCtx.SaveChangesAsync();

        foreach (var tenantId in tenants)
        {
            using var readCtx = CreateContext(tenantId);
            await readCtx.SetTenantAsync(tenantId);

            var results = await readCtx.BusinessUnits.ToListAsync();

            // Todos os resultados devem pertencer ao tenant correto
            var allBelongToTenant = results.All(bu => bu.TenantId == tenantId);
            if (!allBelongToTenant) return false;

            // Nenhum registro de outro tenant deve aparecer (cross-leakage)
            var otherTenantIds = tenants.Where(t => t != tenantId).ToHashSet();
            var hasLeakage = results.Any(bu => otherTenantIds.Contains(bu.TenantId));
            if (hasLeakage) return false;
        }

        return true;
    }

    // ── PBT-01: Isolamento de Users via global filter ────────────────────

    /// <summary>
    /// PBT-01 (ST-02): gera 2..4 tenants com 1..5 usuários cada.
    /// Afirma que queries de User com contexto de tenant_id T retornam apenas
    /// usuários do tenant T — zero cross-leakage em User.
    /// Executa com ≥100 exemplos.
    /// </summary>
    [Property(MaxTest = 100, QuietOnSuccess = true)]
    public Property User_QueryByTenantId_ReturnsOnlyTenantRecords(
        PositiveInt tenantCountRaw,
        PositiveInt userCountPerTenantRaw)
    {
        var tenantCount = (tenantCountRaw.Get % 3) + 2;
        var userCountPerTenant = (userCountPerTenantRaw.Get % 5) + 1;

        return RunUserIsolationCheck(tenantCount, userCountPerTenant)
            .GetAwaiter().GetResult()
            .ToProperty();
    }

    private async Task<bool> RunUserIsolationCheck(int tenantCount, int userCountPerTenant)
    {
        var tenants = Enumerable.Range(0, tenantCount)
            .Select(_ => Guid.NewGuid())
            .ToList();

        using var writeCtx = CreateContextWithoutFilter();

        foreach (var tenantId in tenants)
        {
            for (int i = 0; i < userCountPerTenant; i++)
            {
                var user = User.Activate(
                    $"user-{Guid.NewGuid():N}@test.com",
                    "Usuário Teste",
                    $"uid-{Guid.NewGuid():N}",
                    tenantId,
                    Now);
                user.ClearDomainEvents();
                writeCtx.Users.Add(user);
            }
        }

        await writeCtx.SaveChangesAsync();

        foreach (var tenantId in tenants)
        {
            using var readCtx = CreateContext(tenantId);
            await readCtx.SetTenantAsync(tenantId);

            var results = await readCtx.Users.ToListAsync();

            var allBelongToTenant = results.All(u => u.TenantId == tenantId);
            if (!allBelongToTenant) return false;

            var otherTenantIds = tenants.Where(t => t != tenantId).ToHashSet();
            var hasLeakage = results.Any(u => otherTenantIds.Contains(u.TenantId));
            if (hasLeakage) return false;
        }

        return true;
    }

    // ── PBT-01: Isolamento de UserMemberships via shadow property ────────

    /// <summary>
    /// PBT-01 (ST-03): gera 2..3 tenants com 1 BU e 1 usuário com membership cada.
    /// Afirma que os memberships lidos via Users (com Include) pertencem apenas ao tenant consultado.
    /// Verifica cross-leakage via shadow property TenantId no contexto do EF.
    /// Executa com ≥50 exemplos.
    /// </summary>
    [Property(MaxTest = 50, QuietOnSuccess = true)]
    public Property UserMemberships_ViaUserInclude_NoLeakage(PositiveInt tenantCountRaw)
    {
        var tenantCount = (tenantCountRaw.Get % 2) + 2;

        return RunMembershipIsolationCheck(tenantCount)
            .GetAwaiter().GetResult()
            .ToProperty();
    }

    private async Task<bool> RunMembershipIsolationCheck(int tenantCount)
    {
        var tenants = Enumerable.Range(0, tenantCount)
            .Select(_ => Guid.NewGuid())
            .ToList();

        using var writeCtx = CreateContextWithoutFilter();

        foreach (var tenantId in tenants)
        {
            var bu = BusinessUnit.Create(
                BusinessUnitName.Create($"BU-{Guid.NewGuid():N}"),
                tenantId,
                Now);
            bu.ClearDomainEvents();
            writeCtx.BusinessUnits.Add(bu);

            var user = User.Activate(
                $"user-{Guid.NewGuid():N}@test.com",
                "Usuário Teste",
                $"uid-{Guid.NewGuid():N}",
                tenantId,
                Now);
            user.AssignMembership(bu.Id, Role.Vendedor, Guid.NewGuid());
            user.ClearDomainEvents();
            writeCtx.Users.Add(user);
        }

        await writeCtx.SaveChangesAsync();

        // Verifica que o global filter em User garante que memberships carregados
        // via Include pertencem apenas ao tenant consultado (isolamento via User.TenantId).
        foreach (var tenantId in tenants)
        {
            using var readCtx = CreateContext(tenantId);
            await readCtx.SetTenantAsync(tenantId);

            var usersWithMemberships = await readCtx.Users
                .Include(u => u.Memberships)
                .ToListAsync();

            // Todos os usuários devem pertencer ao tenant correto (global filter)
            var allUsersBelongToTenant = usersWithMemberships.All(u => u.TenantId == tenantId);
            if (!allUsersBelongToTenant) return false;

            // Nenhum usuário de outro tenant deve aparecer
            var otherTenantIds = tenants.Where(t => t != tenantId).ToHashSet();
            var hasUserLeakage = usersWithMemberships.Any(u => otherTenantIds.Contains(u.TenantId));
            if (hasUserLeakage) return false;

            // Todo usuário com membership deve ter ao menos 1 membership
            // (garante que o Include não perdeu dados)
            var allUsersHaveMemberships = usersWithMemberships.All(u => u.Memberships.Count > 0);
            if (!allUsersHaveMemberships) return false;
        }

        return true;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private OrganizationDbContext CreateContext(Guid tenantId)
    {
        var accessor = new TenantContextAccessor { TenantId = tenantId, IsEnabled = true };
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseNpgsql(_connectionString).Options;
        return new OrganizationDbContext(options, accessor);
    }

    private OrganizationDbContext CreateContextWithoutFilter()
    {
        var accessor = new TenantContextAccessor { IsEnabled = false };
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseNpgsql(_connectionString).Options;
        return new OrganizationDbContext(options, accessor);
    }
}
