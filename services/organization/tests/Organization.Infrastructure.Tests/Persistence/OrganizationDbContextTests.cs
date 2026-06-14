using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Organization.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Organization.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de integração do <see cref="OrganizationDbContext"/> via Testcontainers PostgreSQL.
/// Valida: mappings EF Core, global query filter por tenant_id, RLS e migration.
/// TASK-15 (Onda 4 — Infrastructure).
/// </summary>
[Trait("Category", "Integration")]
public sealed class OrganizationDbContextTests : IAsyncLifetime
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

        // Aplicar migration em contexto sem filtro (applyRls=false = IsEnabled=false)
        using var ctx = CreateContextWithoutFilter();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Cria contexto com filtro ativo para o tenant informado.</summary>
    private OrganizationDbContext CreateContext(Guid tenantId)
    {
        var accessor = new TenantContextAccessor { TenantId = tenantId, IsEnabled = true };
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        return new OrganizationDbContext(options, accessor);
    }

    /// <summary>Cria contexto sem global filter (para migrations e setup).</summary>
    private OrganizationDbContext CreateContextWithoutFilter()
    {
        var accessor = new TenantContextAccessor { IsEnabled = false };
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        return new OrganizationDbContext(options, accessor);
    }

    // ── ST-01: Persistência básica e tenant_id em todas as tabelas ─────────

    [Fact(DisplayName = "ST-01: BusinessUnit persiste e é recuperada com tenant_id")]
    public async Task BusinessUnit_PersisteERecupera_ComTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas SP"), tenantId, now);
        bu.ClearDomainEvents();

        // Act — salva sem filter (SET tenant) para inserir com tenant_id correto
        using var writeCtx = CreateContextWithoutFilter();
        writeCtx.BusinessUnits.Add(bu);
        await writeCtx.SaveChangesAsync();

        // Assert — recupera via contexto com filtro ativo
        using var readCtx = CreateContext(tenantId);
        await readCtx.SetTenantAsync(tenantId);
        var found = await readCtx.BusinessUnits.FirstOrDefaultAsync(b => b.Id == bu.Id);
        found.Should().NotBeNull();
        found!.TenantId.Should().Be(tenantId);
        found.Name.Value.Should().Be("Vendas SP");
    }

    [Fact(DisplayName = "ST-01: Stage, OriginChannel e LossReason persistem junto com BusinessUnit")]
    public async Task BusinessUnit_PersistePipelineEntities()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas RJ"), tenantId, now);
        bu.AddStage("Lead", Probability.Create(10), StageCategory.Open, 1, Guid.NewGuid());
        bu.AddStage("Ganho", Probability.Create(100), StageCategory.Won, 7, Guid.NewGuid());
        bu.AddStage("Perdido", Probability.Zero, StageCategory.Lost, 8, Guid.NewGuid());
        bu.AddOriginChannel("Inbound", Guid.NewGuid());
        bu.AddLossReason("Preço alto", Guid.NewGuid());
        bu.ClearDomainEvents();

        // Act
        using var writeCtx = CreateContextWithoutFilter();
        writeCtx.BusinessUnits.Add(bu);
        await writeCtx.SaveChangesAsync();

        // Assert
        using var readCtx = CreateContext(tenantId);
        await readCtx.SetTenantAsync(tenantId);
        var found = await readCtx.BusinessUnits
            .Include(b => b.Stages)
            .Include(b => b.OriginChannels)
            .Include(b => b.LossReasons)
            .FirstOrDefaultAsync(b => b.Id == bu.Id);

        found.Should().NotBeNull();
        found!.Stages.Should().HaveCount(3);
        found.OriginChannels.Should().HaveCount(1);
        found.LossReasons.Should().HaveCount(1);
    }

    // ── ST-03: Global query filter — isolamento cross-tenant via EF Core ───

    [Fact(DisplayName = "ST-03: Global filter bloqueia leitura cross-tenant via EF Core")]
    public async Task GlobalFilter_BloqueiaCrossTenant()
    {
        // Arrange — dois tenants isolados inseridos via contexto sem filtro
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var buA = BusinessUnit.Create(BusinessUnitName.Create("BU-Tenant-A"), tenantA, now);
        buA.ClearDomainEvents();
        var buB = BusinessUnit.Create(BusinessUnitName.Create("BU-Tenant-B"), tenantB, now);
        buB.ClearDomainEvents();

        using var writeCtx = CreateContextWithoutFilter();
        writeCtx.BusinessUnits.Add(buA);
        writeCtx.BusinessUnits.Add(buB);
        await writeCtx.SaveChangesAsync();

        // Act — contexto de tenantA lê apenas seus registros
        using var readCtxA = CreateContext(tenantA);
        await readCtxA.SetTenantAsync(tenantA);
        var resultA = await readCtxA.BusinessUnits.ToListAsync();

        // Assert
        resultA.Should().HaveCount(1, "o global filter deve retornar apenas registros de tenantA");
        resultA.Should().AllSatisfy(b => b.TenantId.Should().Be(tenantA));
        resultA.Should().NotContain(b => b.Id == buB.Id);
    }

    // ── ST-05: RLS via Postgres — acesso com tenant errado retorna vazio ───

    [Fact(DisplayName = "ST-05: RLS bloqueia acesso sem contexto de tenant correto")]
    public async Task Rls_BloqueiaSemContextoTenantCorreto()
    {
        // Arrange — inserir dado via contexto sem filtro
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var bu = BusinessUnit.Create(BusinessUnitName.Create("BU-RLS-Test"), tenantId, now);
        bu.ClearDomainEvents();

        using var writeCtx = CreateContextWithoutFilter();
        writeCtx.BusinessUnits.Add(bu);
        await writeCtx.SaveChangesAsync();

        // Act — contexto com tenant diferente (RLS filtra por tenant_id)
        var wrongTenant = Guid.NewGuid();
        using var ctxRls = CreateContext(wrongTenant);
        await ctxRls.SetTenantAsync(wrongTenant); // SET app.current_tenant = wrongTenant
        var result = await ctxRls.BusinessUnits.ToListAsync();

        // Assert — nem RLS nem global filter devem revelar registros de outro tenant
        result.Should().BeEmpty("RLS e global filter devem bloquear acesso a registros de outro tenant");
    }

    // ── ST-07: Tabelas outbox_events e inbox_messages existem ─────────────

    [Fact(DisplayName = "ST-07: Tabelas outbox_events e inbox_messages existem após migration")]
    public async Task Migration_CriaOutboxEInboxTables()
    {
        // Act — verifica existência das tabelas via EF Core
        using var ctx = CreateContextWithoutFilter();
        var outboxCount = await ctx.OutboxEvents.IgnoreQueryFilters().CountAsync();
        var inboxCount = await ctx.InboxMessages.CountAsync();

        // Assert
        outboxCount.Should().Be(0);
        inboxCount.Should().Be(0);
    }
}
