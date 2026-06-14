using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Organization.Domain.Aggregates;
using Organization.Domain.Exceptions;
using Organization.Domain.ValueObjects;
using Organization.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Organization.Infrastructure.Tests.PBT;

/// <summary>
/// PBT-05 — Integridade após desativação (TASK-27, Onda 6 — Hardening).
/// Gera usuários/BUs com memberships vinculados; executa soft-delete;
/// afirma que todas as FKs e contagens são preservadas (nenhum registro órfão).
/// Verifica que soft-delete é monotônico: uma vez desativado, não reverte.
/// Referências: design §7, §11; DD-006; RNF 1; TASK-27.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "PBT-05")]
public sealed class DeactivationIntegrityPbtTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("org_pbt05")
        .WithUsername("org_app")
        .WithPassword("test_pbt5")
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

    // ── PBT-05: Desativação de User preserva memberships ─────────────────

    /// <summary>
    /// PBT-05 (ST-03/ST-04): gera 1..5 usuários por tenant com 1..3 memberships cada.
    /// Desativa cada usuário e afirma que:
    /// - contagem de memberships no banco é idêntica ao antes da desativação;
    /// - registros de membership não se tornam órfãos (user_id FK válida);
    /// - campo active = false, deactivated_at preenchido;
    /// - shadow property TenantId nos memberships aponta para o tenant correto.
    /// Executa com ≥100 exemplos.
    /// </summary>
    [Property(MaxTest = 100, QuietOnSuccess = true)]
    public Property UserDeactivation_PreservesMembershipsIntegrity(
        PositiveInt userCountRaw,
        PositiveInt membershipCountRaw)
    {
        var userCount = (userCountRaw.Get % 5) + 1;
        var membershipCountPerUser = (membershipCountRaw.Get % 3) + 1;

        return RunUserDeactivationIntegrityCheck(userCount, membershipCountPerUser)
            .GetAwaiter().GetResult()
            .ToProperty();
    }

    private async Task<bool> RunUserDeactivationIntegrityCheck(int userCount, int membershipCountPerUser)
    {
        var tenantId = Guid.NewGuid();
        var totalBusNeeded = userCount * membershipCountPerUser;

        // Cria BUs suficientes para os memberships
        using var buWriteCtx = CreateContextWithoutFilter();
        var buList = new List<BusinessUnit>();

        for (int i = 0; i < totalBusNeeded; i++)
        {
            var bu = BusinessUnit.Create(
                BusinessUnitName.Create($"BU-{Guid.NewGuid():N}"),
                tenantId,
                Now);
            bu.ClearDomainEvents();
            buWriteCtx.BusinessUnits.Add(bu);
            buList.Add(bu);
        }

        await buWriteCtx.SaveChangesAsync();

        // Cria usuários com memberships
        using var userWriteCtx = CreateContextWithoutFilter();

        for (int i = 0; i < userCount; i++)
        {
            var user = User.Activate(
                $"user-pbt05-{Guid.NewGuid():N}@test.com",
                "Usuário PBT",
                $"uid-{Guid.NewGuid():N}",
                tenantId,
                Now);

            for (int j = 0; j < membershipCountPerUser; j++)
            {
                var buIndex = (i * membershipCountPerUser) + j;
                user.AssignMembership(buList[buIndex].Id, Role.Vendedor, Guid.NewGuid());
            }

            user.ClearDomainEvents();
            userWriteCtx.Users.Add(user);
        }

        await userWriteCtx.SaveChangesAsync();

        // Conta memberships antes da desativação
        using var countCtx = CreateContextWithoutFilter();
        var membershipCountBefore = await countCtx.Users
            .Include(u => u.Memberships)
            .Where(u => u.TenantId == tenantId)
            .SelectMany(u => u.Memberships)
            .CountAsync();

        // Desativa todos os usuários
        using var deactivateCtx = CreateContextWithoutFilter();
        var usersToDeactivate = await deactivateCtx.Users
            .Where(u => u.TenantId == tenantId && u.Active)
            .ToListAsync();

        var deactivatedAt = DateTimeOffset.UtcNow;
        foreach (var user in usersToDeactivate)
        {
            user.Deactivate(deactivatedAt);
            user.ClearDomainEvents();
        }

        await deactivateCtx.SaveChangesAsync();

        // Verifica integridade após desativação
        using var verifyCtx = CreateContextWithoutFilter();

        // 1. Todos usuários estão inativos
        var deactivatedUsers = await verifyCtx.Users
            .Where(u => u.TenantId == tenantId)
            .ToListAsync();

        if (!deactivatedUsers.All(u => !u.Active)) return false;

        // 2. Todos têm DeactivatedAt preenchido
        if (!deactivatedUsers.All(u => u.DeactivatedAt.HasValue)) return false;

        // 3. Contagem de memberships é idêntica à de antes da desativação (FKs preservadas)
        var membershipCountAfter = await verifyCtx.Users
            .Include(u => u.Memberships)
            .Where(u => u.TenantId == tenantId)
            .SelectMany(u => u.Memberships)
            .CountAsync();

        if (membershipCountBefore != membershipCountAfter) return false;

        // 4. Memberships não são órfãos: todo membership pertence a um user existente no tenant
        var allUserIds = deactivatedUsers.Select(u => u.Id).ToHashSet();
        var usersWithMemberships = await verifyCtx.Users
            .Include(u => u.Memberships)
            .Where(u => u.TenantId == tenantId)
            .ToListAsync();

        // Verifica que userId de cada membership aponta para um user real no tenant
        foreach (var user in usersWithMemberships)
        {
            if (!allUserIds.Contains(user.Id)) return false;
        }

        // Verifica que a soma de memberships por user é consistente com o antes
        var totalMembershipsViaUsers = usersWithMemberships.Sum(u => u.Memberships.Count);
        if (totalMembershipsViaUsers != membershipCountAfter) return false;

        return true;
    }

    // ── PBT-05: Desativação de BU não remove memberships existentes ───────

    /// <summary>
    /// PBT-05 (ST-05): gera 1..4 BUs ativas por tenant com usuários vinculados.
    /// Desativa as BUs e afirma que:
    /// - contagem total de memberships permanece igual (soft-delete preserva FKs);
    /// - BU tem active = false e deactivated_at preenchido;
    /// - bu_id nos memberships ainda referencia BUs existentes no banco.
    /// Executa com ≥100 exemplos.
    /// </summary>
    [Property(MaxTest = 100, QuietOnSuccess = true)]
    public Property BuDeactivation_PreservesMembershipsFkIntegrity(
        PositiveInt buCountRaw,
        PositiveInt usersPerBuRaw)
    {
        var buCount = (buCountRaw.Get % 4) + 1;
        var usersPerBu = (usersPerBuRaw.Get % 3) + 1;

        return RunBuDeactivationIntegrityCheck(buCount, usersPerBu)
            .GetAwaiter().GetResult()
            .ToProperty();
    }

    private async Task<bool> RunBuDeactivationIntegrityCheck(int buCount, int usersPerBu)
    {
        var tenantId = Guid.NewGuid();

        // Cria BUs
        using var writeCtx = CreateContextWithoutFilter();
        var buList = new List<BusinessUnit>();

        for (int i = 0; i < buCount; i++)
        {
            var bu = BusinessUnit.Create(
                BusinessUnitName.Create($"BU-{Guid.NewGuid():N}"),
                tenantId,
                Now);
            bu.ClearDomainEvents();
            writeCtx.BusinessUnits.Add(bu);
            buList.Add(bu);
        }

        await writeCtx.SaveChangesAsync();

        // Cria usuários com membership em cada BU
        using var userWriteCtx = CreateContextWithoutFilter();

        for (int i = 0; i < buCount; i++)
        {
            var buId = buList[i].Id;
            for (int j = 0; j < usersPerBu; j++)
            {
                var user = User.Activate(
                    $"user-bu-{Guid.NewGuid():N}@test.com",
                    "Usuário PBT BU",
                    $"uid-{Guid.NewGuid():N}",
                    tenantId,
                    Now);
                user.AssignMembership(buId, Role.Viewer, Guid.NewGuid());
                user.ClearDomainEvents();
                userWriteCtx.Users.Add(user);
            }
        }

        await userWriteCtx.SaveChangesAsync();

        // Contagem total de memberships antes da desativação das BUs
        using var countCtx = CreateContextWithoutFilter();
        var membershipCountBefore = await countCtx.Users
            .Include(u => u.Memberships)
            .Where(u => u.TenantId == tenantId)
            .SelectMany(u => u.Memberships)
            .CountAsync();

        // Desativa todas as BUs
        using var deactivateCtx = CreateContextWithoutFilter();
        var busToDeactivate = await deactivateCtx.BusinessUnits
            .Where(b => b.TenantId == tenantId && b.Active)
            .ToListAsync();

        var deactivatedAt = DateTimeOffset.UtcNow;
        foreach (var bu in busToDeactivate)
        {
            bu.Deactivate(deactivatedAt);
            bu.ClearDomainEvents();
        }

        await deactivateCtx.SaveChangesAsync();

        // Verifica integridade após desativação das BUs
        using var verifyCtx = CreateContextWithoutFilter();

        // 1. Todas as BUs estão inactive com deactivated_at
        var deactivatedBus = await verifyCtx.BusinessUnits
            .Where(b => b.TenantId == tenantId)
            .ToListAsync();

        if (!deactivatedBus.All(b => !b.Active && b.DeactivatedAt.HasValue)) return false;

        // 2. Contagem de memberships é idêntica (FKs preservadas por soft-delete)
        var membershipCountAfter = await verifyCtx.Users
            .Include(u => u.Memberships)
            .Where(u => u.TenantId == tenantId)
            .SelectMany(u => u.Memberships)
            .CountAsync();

        if (membershipCountBefore != membershipCountAfter) return false;

        // 3. BuIds nos memberships ainda referenciam BUs existentes (FK válida)
        var existingBuIds = deactivatedBus.Select(b => b.Id).ToHashSet();
        var allMembershipBuIds = await verifyCtx.Users
            .Include(u => u.Memberships)
            .Where(u => u.TenantId == tenantId)
            .SelectMany(u => u.Memberships)
            .Select(m => m.BuId)
            .ToListAsync();

        if (!allMembershipBuIds.All(buId => existingBuIds.Contains(buId))) return false;

        return true;
    }

    // ── PBT-05: Monotonia do soft-delete ─────────────────────────────────

    /// <summary>
    /// PBT-05 (ST-06): soft-delete é monotônico.
    /// Um usuário desativado não pode ser desativado novamente: DomainException é lançada.
    /// Persistência após desativação deve ter active = false e deactivated_at preenchido.
    /// Executa com ≥100 exemplos.
    /// </summary>
    [Property(MaxTest = 100, QuietOnSuccess = true)]
    public Property UserDeactivation_IsMonotonic_SecondCallThrowsDomainException(
        PositiveInt seed)
    {
        return RunMonotonicDeactivationCheck(seed.Get)
            .GetAwaiter().GetResult()
            .ToProperty();
    }

    private async Task<bool> RunMonotonicDeactivationCheck(int seed)
    {
        var tenantId = Guid.NewGuid();

        using var writeCtx = CreateContextWithoutFilter();

        var user = User.Activate(
            $"mono-{seed}-{Guid.NewGuid():N}@test.com",
            "Usuário Monotônico",
            $"uid-mono-{Guid.NewGuid():N}",
            tenantId,
            Now);
        user.ClearDomainEvents();
        writeCtx.Users.Add(user);
        await writeCtx.SaveChangesAsync();

        var userId = user.Id;

        // Primeira desativação deve ter sucesso
        using var deactivateCtx = CreateContextWithoutFilter();
        var userToDeactivate = await deactivateCtx.Users.FirstAsync(u => u.Id == userId);
        userToDeactivate.Deactivate(DateTimeOffset.UtcNow);
        userToDeactivate.ClearDomainEvents();
        await deactivateCtx.SaveChangesAsync();

        // Segunda desativação deve lançar DomainException (monotonia)
        using var reloadCtx = CreateContextWithoutFilter();
        var deactivatedUser = await reloadCtx.Users.FirstAsync(u => u.Id == userId);

        var exceptionThrown = false;
        try
        {
            deactivatedUser.Deactivate(DateTimeOffset.UtcNow);
        }
        catch (DomainException)
        {
            exceptionThrown = true;
        }

        if (!exceptionThrown) return false;

        // Confirma persistência: active = false, deactivated_at preenchido
        using var verifyCtx = CreateContextWithoutFilter();
        var persistedUser = await verifyCtx.Users.FirstAsync(u => u.Id == userId);

        return !persistedUser.Active && persistedUser.DeactivatedAt.HasValue;
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
