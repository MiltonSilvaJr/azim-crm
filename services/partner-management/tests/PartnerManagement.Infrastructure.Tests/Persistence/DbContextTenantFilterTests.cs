using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.ValueObjects;
using PartnerManagement.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace PartnerManagement.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de integração para o filtro global de tenant no <see cref="PartnerManagementDbContext"/>.
/// Verifica que: inserir dois parceiros de tenants distintos → query sem filtro explícito retorna
/// apenas os do tenant do contexto corrente (TASK-15 ST-01).
/// Usa PostgreSQL real via Testcontainers (design §13, ADR-0001).
/// </summary>
[Trait("Category", "Integration")]
public sealed class DbContextTenantFilterTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private string _connectionString = null!;

    public DbContextTenantFilterTests()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("partner_management_test_task15")
            .WithUsername("app_test")
            .WithPassword("test_secret")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        // Cria o schema usando EnsureCreated (suficiente para testes de integração)
        using PartnerManagementDbContext ctx = CreateDbContext(Guid.NewGuid());
        await ctx.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private PartnerManagementDbContext CreateDbContext(Guid tenantId)
    {
        DbContextOptions<PartnerManagementDbContext> options = new DbContextOptionsBuilder<PartnerManagementDbContext>()
            .UseNpgsql(_connectionString)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new PartnerManagementDbContext(options, new StaticTenantContext(tenantId));
    }

    /// <summary>
    /// TASK-15 ST-01 (Green): inserir parceiros de dois tenants distintos;
    /// query no contexto do tenant A retorna apenas os parceiros do tenant A.
    /// </summary>
    [Fact(DisplayName = "TASK-15: filtro global de tenant retorna apenas parceiros do tenant corrente")]
    public async Task GlobalTenantFilter_ReturnsOnlyCurrentTenantPartners()
    {
        // Arrange
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();
        Guid actor = Guid.NewGuid();

        ICanonicalRoleProvider roleProvider = new AlwaysValidRoleProvider();

        Partner partnerOfA = Partner.Create(
            tenantA, "Parceiro Alfa", "Indicador",
            CommissionDefaults.Default, null, null, roleProvider, actor);

        Partner partnerOfB = Partner.Create(
            tenantB, "Parceiro Beta", "Revendedor",
            CommissionDefaults.Default, null, null, roleProvider, actor);

        // Persiste parceiro do tenant A
        await using (PartnerManagementDbContext ctxA = CreateDbContext(tenantA))
        {
            await ctxA.AddPartnerAsync(partnerOfA);
            await ctxA.SaveChangesAsync();
        }

        // Persiste parceiro do tenant B
        await using (PartnerManagementDbContext ctxB = CreateDbContext(tenantB))
        {
            await ctxB.AddPartnerAsync(partnerOfB);
            await ctxB.SaveChangesAsync();
        }

        // Act — query no contexto do tenant A (sem filtro explícito de tenant)
        await using PartnerManagementDbContext ctxARead = CreateDbContext(tenantA);
        List<Partner> result = await ctxARead.GetPartners().ToListAsync();

        // Assert — apenas o parceiro do tenant A deve aparecer
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(partnerOfA.Id);
        result[0].TenantId.Should().Be(tenantA);
    }

    /// <summary>
    /// TASK-15: contexto do tenant B não enxerga parceiro do tenant A.
    /// </summary>
    [Fact(DisplayName = "TASK-15: contexto de tenant B não enxerga parceiros de tenant A")]
    public async Task GlobalTenantFilter_TenantBCannotSeeTenantAPartners()
    {
        // Arrange
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();
        Guid actor = Guid.NewGuid();
        ICanonicalRoleProvider roleProvider = new AlwaysValidRoleProvider();

        Partner partnerOfA = Partner.Create(
            tenantA, "Parceiro Gama", "Distribuidor",
            CommissionDefaults.Default, null, null, roleProvider, actor);

        await using (PartnerManagementDbContext ctxA = CreateDbContext(tenantA))
        {
            await ctxA.AddPartnerAsync(partnerOfA);
            await ctxA.SaveChangesAsync();
        }

        // Act — tenta encontrar o parceiro do tenant A no contexto do tenant B
        await using PartnerManagementDbContext ctxB = CreateDbContext(tenantB);
        Partner? found = await ctxB.GetPartners().FirstOrDefaultAsync(p => p.Id == partnerOfA.Id);

        // Assert — não deve encontrar
        found.Should().BeNull();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private sealed class AlwaysValidRoleProvider : ICanonicalRoleProvider
    {
        public bool IsCanonical(string role, Guid tenantId) => true;
    }

    private sealed class StaticTenantContext : Application.Ports.ITenantContext
    {
        public StaticTenantContext(Guid tenantId) => CurrentTenantId = tenantId;
        public Guid CurrentTenantId { get; }
    }
}
