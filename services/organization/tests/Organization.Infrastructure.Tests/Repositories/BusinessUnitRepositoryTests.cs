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
/// Testes de integração de <see cref="BusinessUnitRepository"/> via Testcontainers PostgreSQL.
/// Valida: isolamento por tenant, persistência com pipeline entities, consultas.
/// TASK-16 (Onda 4 — Infrastructure).
/// </summary>
[Trait("Category", "Integration")]
public sealed class BusinessUnitRepositoryTests : IAsyncLifetime
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

    // ── Helpers ────────────────────────────────────────────────────────────

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

    private (BusinessUnitRepository repo, OrganizationDbContext ctx) CreateRepo(Guid tenantId)
    {
        var ctx = CreateContext(tenantId);
        var repo = new BusinessUnitRepository(ctx);
        return (repo, ctx);
    }

    // ── ST-01: GetByName respeita tenant_id ─────────────────────────────

    [Fact(DisplayName = "ST-01: GetByName retorna BU do tenant correto")]
    public async Task GetByName_RetornaBuDoTenantCorreto()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var buA = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), tenantA, now);
        buA.ClearDomainEvents();
        var buB = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), tenantB, now);
        buB.ClearDomainEvents();

        using var write = CreateContextWithoutFilter();
        write.BusinessUnits.AddRange(buA, buB);
        await write.SaveChangesAsync();

        // Act — repositório de tenantA não deve ver BU de tenantB
        var (repo, ctx) = CreateRepo(tenantA);
        using (ctx)
        {
            await ctx.SetTenantAsync(tenantA);
            var found = await repo.GetByIdAsync(buA.Id);
            var notFound = await repo.GetByIdAsync(buB.Id);

            // Assert
            found.Should().NotBeNull();
            found!.TenantId.Should().Be(tenantA);
            notFound.Should().BeNull("repositório de tenantA não pode ver BU de tenantB");
        }
    }

    [Fact(DisplayName = "ST-01: ExistsByNameAsync respeita tenant_id")]
    public async Task ExistsByName_RespeitaTenantId()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var buA = BusinessUnit.Create(BusinessUnitName.Create("Comercial"), tenantA, now);
        buA.ClearDomainEvents();

        using var write = CreateContextWithoutFilter();
        write.BusinessUnits.Add(buA);
        await write.SaveChangesAsync();

        // Act
        var (repoA, ctxA) = CreateRepo(tenantA);
        var (repoB, ctxB) = CreateRepo(tenantB);
        using (ctxA)
        using (ctxB)
        {
            await ctxA.SetTenantAsync(tenantA);
            await ctxB.SetTenantAsync(tenantB);

            var existsInA = await repoA.ExistsByNameAsync("COMERCIAL");
            var existsInB = await repoB.ExistsByNameAsync("COMERCIAL");

            // Assert
            existsInA.Should().BeTrue("BU existe no tenantA");
            existsInB.Should().BeFalse("BU não existe no tenantB");
        }
    }

    // ── ST-01: Save persiste BU com entidades de pipeline ───────────────

    [Fact(DisplayName = "ST-01: SaveAsync persiste BU com Stage, OriginChannel e LossReason")]
    public async Task Save_PersisteBuComPipelineEntities()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Pipeline Test"), tenantId, now);
        bu.AddStage("Lead", Probability.Create(10), StageCategory.Open, 1, Guid.NewGuid());
        bu.AddStage("Ganho", Probability.Create(100), StageCategory.Won, 2, Guid.NewGuid());
        bu.AddStage("Perdido", Probability.Zero, StageCategory.Lost, 3, Guid.NewGuid());
        bu.AddOriginChannel("Referral", Guid.NewGuid());
        bu.AddLossReason("Concorrente mais barato", Guid.NewGuid());
        bu.ClearDomainEvents();

        // Act
        var (repo, ctx) = CreateRepo(tenantId);
        using (ctx)
        {
            await ctx.SetTenantAsync(tenantId);
            await repo.SaveAsync(bu);
            await ctx.SaveChangesAsync();
        }

        // Assert
        var (repoRead, ctxRead) = CreateRepo(tenantId);
        using (ctxRead)
        {
            await ctxRead.SetTenantAsync(tenantId);
            var found = await repoRead.GetByIdAsync(bu.Id);
            found.Should().NotBeNull();
            found!.Stages.Should().HaveCount(3);
            found.OriginChannels.Should().HaveCount(1);
            found.LossReasons.Should().HaveCount(1);
        }
    }

    // ── ListActiveAsync — paginação ──────────────────────────────────────

    [Fact(DisplayName = "ST-01: ListActiveAsync retorna BUs ativas paginadas do tenant")]
    public async Task ListActiveAsync_RetornaBusAtivasPaginadas()
    {
        // Arrange — 3 BUs ativas + 1 inativa no mesmo tenant
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var bu1 = BusinessUnit.Create(BusinessUnitName.Create("BU-1"), tenantId, now);
        var bu2 = BusinessUnit.Create(BusinessUnitName.Create("BU-2"), tenantId, now);
        var bu3 = BusinessUnit.Create(BusinessUnitName.Create("BU-3"), tenantId, now);
        var bu4 = BusinessUnit.Create(BusinessUnitName.Create("BU-4"), tenantId, now);
        bu4.Deactivate(now);
        new[] { bu1, bu2, bu3, bu4 }.ToList().ForEach(b => b.ClearDomainEvents());

        using var write = CreateContextWithoutFilter();
        write.BusinessUnits.AddRange(bu1, bu2, bu3, bu4);
        await write.SaveChangesAsync();

        // Act
        var (repo, ctx) = CreateRepo(tenantId);
        using (ctx)
        {
            await ctx.SetTenantAsync(tenantId);
            var page1 = await repo.ListActiveAsync(page: 1, pageSize: 2);
            var page2 = await repo.ListActiveAsync(page: 2, pageSize: 2);

            // Assert
            page1.Should().HaveCount(2);
            page2.Should().HaveCount(1, "apenas 3 BUs ativas no total");
            page1.Concat(page2).Should().NotContain(b => b.Id == bu4.Id, "BU inativa excluída");
        }
    }
}
