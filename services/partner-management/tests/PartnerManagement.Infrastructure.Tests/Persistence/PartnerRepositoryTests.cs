using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.ValueObjects;
using PartnerManagement.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace PartnerManagement.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de integração para <see cref="PartnerRepository"/> (TASK-17).
/// Cobre: Add + SaveChanges, GetByIdAsync, isolamento cross-tenant, FindByNameAsync, ListAsync.
/// Usa PostgreSQL real via Testcontainers e esquema criado com EnsureCreated.
/// Mapeia: design §6.1, RNF 1, DD-001, ADR-0001, TASK-17.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PartnerRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private string _connectionString = null!;

    private static readonly ICanonicalRoleProvider RoleProvider = new AlwaysValidRoleProvider();
    private static readonly Guid ActorId = Guid.NewGuid();

    // ZeroCommission() é um singleton estático — usar sempre instâncias novas
    // para evitar conflito de identidade de owned entity no EF Core change tracker.
    private static CommissionDefaults ZeroCommission() =>
        CommissionDefaults.Create(Percentage.Create(0m), Percentage.Create(0m));

    public PartnerRepositoryTests()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("partner_management_repo_test")
            .WithUsername("app_test")
            .WithPassword("test_secret")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        // Cria schema com EnsureCreated (CHECKs e RLS testados nos MigrationAndRlsTests)
        using PartnerManagementDbContext ctx = CreateDbContext(Guid.NewGuid());
        await ctx.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    // =========================================================================
    // AddAsync / GetByIdAsync
    // =========================================================================

    /// <summary>
    /// TASK-17 ST-01: adicionar parceiro e recuperar pelo ID no mesmo tenant.
    /// </summary>
    [Fact(DisplayName = "TASK-17: AddAsync e GetByIdAsync retornam parceiro do mesmo tenant")]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsSamePartner()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();

        Partner partner = Partner.Create(
            tenantId, "Parceiro Integração", "Indicador",
            ZeroCommission(), null, null, RoleProvider, ActorId);

        // Act
        await using PartnerManagementDbContext ctxWrite = CreateDbContext(tenantId);
        PartnerRepository repo = new(ctxWrite);
        await repo.AddAsync(partner);
        await ctxWrite.SaveChangesAsync();

        await using PartnerManagementDbContext ctxRead = CreateDbContext(tenantId);
        PartnerRepository repoRead = new(ctxRead);
        Partner? found = await repoRead.GetByIdAsync(partner.Id);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(partner.Id);
        found.Name.Value.Should().Be("Parceiro Integração");
        found.TenantId.Should().Be(tenantId);
    }

    /// <summary>
    /// TASK-17 ST-01: GetByIdAsync em outro tenant não retorna o parceiro (cross-tenant isolation).
    /// </summary>
    [Fact(DisplayName = "TASK-17: GetByIdAsync não retorna parceiro de tenant diferente")]
    public async Task GetByIdAsync_CrossTenant_ReturnsNull()
    {
        // Arrange
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();

        Partner partner = Partner.Create(
            tenantA, "Parceiro do Tenant A", "Revendedor",
            ZeroCommission(), null, null, RoleProvider, ActorId);

        await using (PartnerManagementDbContext ctxA = CreateDbContext(tenantA))
        {
            PartnerRepository repoA = new(ctxA);
            await repoA.AddAsync(partner);
            await ctxA.SaveChangesAsync();
        }

        // Act — tenta encontrar no contexto do tenant B
        await using PartnerManagementDbContext ctxB = CreateDbContext(tenantB);
        PartnerRepository repoB = new(ctxB);
        Partner? result = await repoB.GetByIdAsync(partner.Id);

        // Assert — isolamento cross-tenant: retorna null
        result.Should().BeNull("o filtro global de tenant deve impedir acesso cross-tenant");
    }

    // =========================================================================
    // FindByNameAsync
    // =========================================================================

    /// <summary>
    /// TASK-17: FindByNameAsync retorna somente parceiros do tenant corrente com nome correspondente.
    /// </summary>
    [Fact(DisplayName = "TASK-17: FindByNameAsync retorna parceiros do tenant com nome matching")]
    public async Task FindByNameAsync_ReturnsTenantPartnersWithMatchingName()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();

        Partner p1 = Partner.Create(tenantId, "Alpha Corp", "Indicador", ZeroCommission(), null, null, RoleProvider, ActorId);
        Partner p2 = Partner.Create(tenantId, "Alpha Tech", "Distribuidor", ZeroCommission(), null, null, RoleProvider, ActorId);
        Partner p3 = Partner.Create(tenantId, "Beta Solutions", "Revendedor", ZeroCommission(), null, null, RoleProvider, ActorId);

        await using (PartnerManagementDbContext ctxWrite = CreateDbContext(tenantId))
        {
            PartnerRepository repo = new(ctxWrite);
            await repo.AddAsync(p1);
            await repo.AddAsync(p2);
            await repo.AddAsync(p3);
            await ctxWrite.SaveChangesAsync();
        }

        // Act — busca parceiros cujo nome faz match com "alpha%"
        await using PartnerManagementDbContext ctxRead = CreateDbContext(tenantId);
        PartnerRepository repoRead = new(ctxRead);
        IReadOnlyList<Partner> result = await repoRead.FindByNameAsync("alpha%");

        // Assert — apenas Alpha Corp e Alpha Tech
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(p => p.Name.Value.Should().StartWith("Alpha", "ILike case-insensitive"));
    }

    /// <summary>
    /// TASK-17: FindByNameAsync não retorna parceiros de outro tenant mesmo com nome idêntico.
    /// </summary>
    [Fact(DisplayName = "TASK-17: FindByNameAsync não vaza parceiros de outro tenant")]
    public async Task FindByNameAsync_CrossTenant_DoesNotLeak()
    {
        // Arrange
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();

        Partner partnerA = Partner.Create(tenantA, "Gamma Inc", "Indicador", ZeroCommission(), null, null, RoleProvider, ActorId);
        Partner partnerB = Partner.Create(tenantB, "Gamma Inc", "Indicador", ZeroCommission(), null, null, RoleProvider, ActorId);

        await using (PartnerManagementDbContext ctxA = CreateDbContext(tenantA))
        {
            PartnerRepository repoA = new(ctxA);
            await repoA.AddAsync(partnerA);
            await ctxA.SaveChangesAsync();
        }

        await using (PartnerManagementDbContext ctxB = CreateDbContext(tenantB))
        {
            PartnerRepository repoB = new(ctxB);
            await repoB.AddAsync(partnerB);
            await ctxB.SaveChangesAsync();
        }

        // Act — busca no contexto do tenant A
        await using PartnerManagementDbContext ctxARead = CreateDbContext(tenantA);
        PartnerRepository repoARead = new(ctxARead);
        IReadOnlyList<Partner> result = await repoARead.FindByNameAsync("Gamma Inc");

        // Assert — apenas o parceiro do tenant A
        result.Should().HaveCount(1);
        result[0].TenantId.Should().Be(tenantA);
    }

    // =========================================================================
    // UpdateAsync
    // =========================================================================

    /// <summary>
    /// TASK-17: UpdateAsync persiste alterações no parceiro.
    /// </summary>
    [Fact(DisplayName = "TASK-17: UpdateAsync persiste alteração de parceiro")]
    public async Task UpdateAsync_PersistsChanges()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();

        Partner partner = Partner.Create(
            tenantId, "Parceiro Original", "Indicador",
            ZeroCommission(), null, null, RoleProvider, ActorId);

        await using (PartnerManagementDbContext ctxWrite = CreateDbContext(tenantId))
        {
            PartnerRepository repo = new(ctxWrite);
            await repo.AddAsync(partner);
            await ctxWrite.SaveChangesAsync();
        }

        // Act — atualiza para inativo
        await using (PartnerManagementDbContext ctxUpdate = CreateDbContext(tenantId))
        {
            PartnerRepository repoUpdate = new(ctxUpdate);
            Partner? toUpdate = await repoUpdate.GetByIdAsync(partner.Id);
            toUpdate!.Deactivate();
            await repoUpdate.UpdateAsync(toUpdate);
            await ctxUpdate.SaveChangesAsync();
        }

        // Assert
        await using PartnerManagementDbContext ctxRead = CreateDbContext(tenantId);
        PartnerRepository repoRead = new(ctxRead);
        Partner? found = await repoRead.GetByIdAsync(partner.Id);
        found!.Status.Should().Be(PartnerStatus.Inactive);
    }

    // =========================================================================
    // ListAsync
    // =========================================================================

    /// <summary>
    /// TASK-17: ListAsync com filtro de ativo retorna apenas parceiros ativos.
    /// </summary>
    [Fact(DisplayName = "TASK-17: ListAsync filtra por status ativo")]
    public async Task ListAsync_ActiveFilter_ReturnsOnlyActivePartners()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();

        Partner active = Partner.Create(tenantId, "Ativo Ltda", "Indicador", ZeroCommission(), null, null, RoleProvider, ActorId);
        Partner inactive = Partner.Create(tenantId, "Inativo SA", "Revendedor", ZeroCommission(), null, null, RoleProvider, ActorId);
        inactive.Deactivate();

        await using (PartnerManagementDbContext ctxWrite = CreateDbContext(tenantId))
        {
            PartnerRepository repo = new(ctxWrite);
            await repo.AddAsync(active);
            await repo.AddAsync(inactive);
            await ctxWrite.SaveChangesAsync();
        }

        // Act
        await using PartnerManagementDbContext ctxRead = CreateDbContext(tenantId);
        PartnerRepository repoRead = new(ctxRead);
        (IReadOnlyList<Partner> partners, int total) = await repoRead.ListAsync(
            active: true, triagePending: false, page: 1, pageSize: 10);

        // Assert
        partners.Should().AllSatisfy(p => p.Status.Should().Be(PartnerStatus.Active));
        total.Should().BeGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// TASK-17: ListAsync com triagePending retorna apenas parceiros com percentuais zerados.
    /// </summary>
    [Fact(DisplayName = "TASK-17: ListAsync triagePending retorna parceiros com comissão zerada")]
    public async Task ListAsync_TriagePending_ReturnsPartnersWithZeroCommission()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();

        Partner zeroPct = Partner.Create(tenantId, "Triagem Pendente", "Indicador",
            ZeroCommission(), null, null, RoleProvider, ActorId);

        Partner withPct = Partner.Create(tenantId, "Com Comissão", "Revendedor",
            CommissionDefaults.Create(Percentage.Create(10m), Percentage.Create(5m)),
            null, null, RoleProvider, ActorId);

        await using (PartnerManagementDbContext ctxWrite = CreateDbContext(tenantId))
        {
            PartnerRepository repo = new(ctxWrite);
            await repo.AddAsync(zeroPct);
            await repo.AddAsync(withPct);
            await ctxWrite.SaveChangesAsync();
        }

        // Act
        await using PartnerManagementDbContext ctxRead = CreateDbContext(tenantId);
        PartnerRepository repoRead = new(ctxRead);
        (IReadOnlyList<Partner> partners, int total) = await repoRead.ListAsync(
            active: null, triagePending: true, page: 1, pageSize: 10);

        // Assert — apenas "Triagem Pendente" tem pct zerado
        partners.Should().AllSatisfy(p =>
        {
            p.CommissionDefaults.PctSetup.Value.Should().Be(0m);
            p.CommissionDefaults.PctRecorrente.Value.Should().Be(0m);
        });
        total.Should().BeGreaterThanOrEqualTo(1);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private PartnerManagementDbContext CreateDbContext(Guid tenantId)
    {
        DbContextOptions<PartnerManagementDbContext> options = new DbContextOptionsBuilder<PartnerManagementDbContext>()
            .UseNpgsql(_connectionString)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new PartnerManagementDbContext(options, new StaticTenantContext(tenantId));
    }

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
