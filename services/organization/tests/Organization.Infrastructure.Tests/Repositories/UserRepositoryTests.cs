using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Organization.Infrastructure.Persistence;
using Organization.Infrastructure.Repositories;
using Testcontainers.PostgreSql;
using Xunit;

namespace Organization.Infrastructure.Tests.Repositories;

/// <summary>
/// Testes de integração de <see cref="UserRepository"/> via Testcontainers PostgreSQL.
/// Valida: GetByEmail por tenant, GetActiveTenantAdmins, TenantAdminCounter.
/// TASK-16 (Onda 4 — Infrastructure).
/// </summary>
[Trait("Category", "Integration")]
public sealed class UserRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("org_test")
        .WithUsername("org_app")
        .WithPassword("test_pass")
        .Build();

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        using var ctx = CreateContextWithoutFilter();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private OrganizationDbContext CreateContext(Guid tenantId)
    {
        var accessor = new TenantContextAccessor { TenantId = tenantId, IsEnabled = true };
        var options = new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(_connectionString).Options;
        return new OrganizationDbContext(options, accessor);
    }

    private OrganizationDbContext CreateContextWithoutFilter()
    {
        var accessor = new TenantContextAccessor { IsEnabled = false };
        var options = new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(_connectionString).Options;
        return new OrganizationDbContext(options, accessor);
    }

    private User CreateUser(string email, Guid tenantId, DateTimeOffset now, bool active = true)
    {
        var u = User.Activate(email, "Display Name", $"uid-{Guid.NewGuid()}", tenantId, now);
        u.ClearDomainEvents();
        return u;
    }

    // ── ST-03: GetByEmail por tenant ─────────────────────────────────────

    [Fact(DisplayName = "ST-03: GetByEmailAsync respeita tenant_id")]
    public async Task GetByEmail_RespeitaTenantId()
    {
        // Arrange — mesmo e-mail em dois tenants diferentes
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var userA = CreateUser("admin@example.com", tenantA, now);
        var userB = CreateUser("admin@example.com", tenantB, now);

        using var write = CreateContextWithoutFilter();
        write.Users.AddRange(userA, userB);
        await write.SaveChangesAsync();

        // Act
        using var ctxA = CreateContext(tenantA);
        await ctxA.SetTenantAsync(tenantA);
        var repoA = new UserRepository(ctxA);
        var foundA = await repoA.GetByEmailAsync("admin@example.com");
        var notCrossA = await repoA.GetByIdAsync(userB.Id);

        // Assert
        foundA.Should().NotBeNull();
        foundA!.TenantId.Should().Be(tenantA);
        notCrossA.Should().BeNull("não pode retornar usuário de outro tenant");
    }

    // ── ST-03: GetActiveTenantAdmins ─────────────────────────────────────

    [Fact(DisplayName = "ST-03: GetActiveTenantAdminsAsync retorna apenas TAdmins ativos")]
    public async Task GetActiveTenantAdmins_RetornaApenasAdminsAtivos()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Admin ativo com papel TAdmin
        var admin1 = CreateUser("admin1@example.com", tenantId, now);
        admin1.AssignMembership(buId, Role.TAdmin, Guid.NewGuid());

        // Gestor (não é TAdmin)
        var gestor = CreateUser("gestor@example.com", tenantId, now);
        gestor.AssignMembership(buId, Role.GestorBU, Guid.NewGuid());

        // Admin desativado
        var adminInativo = CreateUser("old-admin@example.com", tenantId, now);
        adminInativo.AssignMembership(buId, Role.TAdmin, Guid.NewGuid());
        adminInativo.Deactivate(now);

        new[] { admin1, gestor, adminInativo }.ToList().ForEach(u => u.ClearDomainEvents());

        using var write = CreateContextWithoutFilter();
        write.Users.AddRange(admin1, gestor, adminInativo);
        await write.SaveChangesAsync();

        // Act
        using var ctx = CreateContext(tenantId);
        await ctx.SetTenantAsync(tenantId);
        var repo = new UserRepository(ctx);
        var admins = await repo.GetActiveTenantAdminsAsync();

        // Assert
        admins.Should().HaveCount(1, "apenas 1 TAdmin ativo");
        admins[0].Id.Should().Be(admin1.Id);
    }

    // ── TenantAdminCounter ───────────────────────────────────────────────

    [Fact(DisplayName = "ST-04: TenantAdminCounter conta corretamente TAdmins ativos")]
    public async Task TenantAdminCounter_ContaCorretamente()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var admin1 = CreateUser("a1@test.com", tenantId, now);
        admin1.AssignMembership(buId, Role.TAdmin, Guid.NewGuid());

        var admin2 = CreateUser("a2@test.com", tenantId, now);
        admin2.AssignMembership(buId, Role.TAdmin, Guid.NewGuid());

        new[] { admin1, admin2 }.ToList().ForEach(u => u.ClearDomainEvents());

        using var write = CreateContextWithoutFilter();
        write.Users.AddRange(admin1, admin2);
        await write.SaveChangesAsync();

        // Act
        using var ctx = CreateContext(tenantId);
        await ctx.SetTenantAsync(tenantId);
        var counter = new TenantAdminCounterAdapter(ctx);
        var count = await counter.CountActiveTenantAdminsAsync(tenantId);

        // Assert
        count.Should().Be(2);
    }
}
