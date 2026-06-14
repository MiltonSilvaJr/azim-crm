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
/// Testes de integração de <see cref="UserInvitationRepository"/> via Testcontainers PostgreSQL.
/// Valida: GetByTokenHash, IsEmailActiveUser, target_memberships serializado em JSONB.
/// TASK-16 (Onda 4 — Infrastructure).
/// </summary>
[Trait("Category", "Integration")]
public sealed class UserInvitationRepositoryTests : IAsyncLifetime
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

    private static UserInvitation CreateInvitation(Guid tenantId, string email, string tokenHash, IEnumerable<(Guid buId, Role role)> memberships)
    {
        var token = InvitationToken.FromHash(tokenHash);
        var now = DateTimeOffset.UtcNow;
        var inv = UserInvitation.Create(
            email,
            token,
            tenantId,
            memberships,
            now.AddHours(72),
            now);
        inv.ClearDomainEvents();
        return inv;
    }

    // ── ST-05: GetByTokenHash por tenant ─────────────────────────────────

    [Fact(DisplayName = "ST-05: GetByTokenHashAsync resolve hash corretamente por tenant")]
    public async Task GetByTokenHash_ResolveHashPorTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var tokenHash = "sha256-abc123-hash-value";
        var invitation = CreateInvitation(tenantId, "user@test.com", tokenHash, [(buId, Role.Vendedor)]);

        using var write = CreateContextWithoutFilter();
        write.UserInvitations.Add(invitation);
        await write.SaveChangesAsync();

        // Act
        using var ctx = CreateContext(tenantId);
        await ctx.SetTenantAsync(tenantId);
        var repo = new UserInvitationRepository(ctx);
        var found = await repo.GetByTokenHashAsync(tokenHash);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(invitation.Id);
        found.TenantId.Should().Be(tenantId);
    }

    [Fact(DisplayName = "ST-05: GetByTokenHashAsync não retorna convite de outro tenant")]
    public async Task GetByTokenHash_NaoRetornaDeOutroTenant()
    {
        // Arrange — mesmo token hash em dois tenants
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var tokenHash = "shared-hash-value";

        var invA = CreateInvitation(tenantA, "user@tenantA.com", tokenHash, [(buId, Role.Viewer)]);
        var invB = CreateInvitation(tenantB, "user@tenantB.com", tokenHash, [(buId, Role.Viewer)]);

        using var write = CreateContextWithoutFilter();
        write.UserInvitations.AddRange(invA, invB);
        await write.SaveChangesAsync();

        // Act — busca no tenantA
        using var ctxA = CreateContext(tenantA);
        await ctxA.SetTenantAsync(tenantA);
        var repoA = new UserInvitationRepository(ctxA);
        var found = await repoA.GetByTokenHashAsync(tokenHash);

        // Assert
        found.Should().NotBeNull();
        found!.TenantId.Should().Be(tenantA, "só deve retornar o convite do tenantA");
    }

    // ── ST-05: target_memberships serializado em JSONB ───────────────────

    [Fact(DisplayName = "ST-05: target_memberships serializado e desserializado corretamente")]
    public async Task TargetMemberships_SerializadoEmJsonb()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId1 = Guid.NewGuid();
        var buId2 = Guid.NewGuid();

        var invitation = CreateInvitation(
            tenantId,
            "multi@test.com",
            "hash-multi",
            [(buId1, Role.Vendedor), (buId2, Role.GestorBU)]);

        using var write = CreateContextWithoutFilter();
        write.UserInvitations.Add(invitation);
        await write.SaveChangesAsync();

        // Act
        using var ctx = CreateContext(tenantId);
        await ctx.SetTenantAsync(tenantId);
        var repo = new UserInvitationRepository(ctx);
        var found = await repo.GetByTokenHashAsync("hash-multi");

        // Assert
        found.Should().NotBeNull();
        // Os TargetMemberships devem ser restaurados do JSONB
        found!.TargetMemberships.Should().HaveCount(2);
        found.TargetMemberships.Should().Contain(m => m.BuId == buId1 && m.Role == Role.Vendedor);
        found.TargetMemberships.Should().Contain(m => m.BuId == buId2 && m.Role == Role.GestorBU);
    }

    // ── IsEmailActiveUser ────────────────────────────────────────────────

    [Fact(DisplayName = "ST-05: IsEmailActiveUserAsync verifica e-mail ativo no tenant")]
    public async Task IsEmailActiveUser_VerificaEmailAtivo()
    {
        // Arrange — usuário ativo no tenant
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var user = User.Activate("active@test.com", "Active User", "uid-active", tenantId, now);
        user.ClearDomainEvents();

        using var write = CreateContextWithoutFilter();
        write.Users.Add(user);
        await write.SaveChangesAsync();

        // Act
        using var ctx = CreateContext(tenantId);
        await ctx.SetTenantAsync(tenantId);
        var repo = new UserInvitationRepository(ctx);

        var activeExists = await repo.IsEmailActiveUserAsync("active@test.com");
        var notExists = await repo.IsEmailActiveUserAsync("unknown@test.com");

        // Assert
        activeExists.Should().BeTrue();
        notExists.Should().BeFalse();
    }
}
