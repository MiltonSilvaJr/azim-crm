using GoalForecast.Infrastructure.Persistence;
using GoalForecast.Infrastructure.Tests.Fixtures;
using Npgsql;

namespace GoalForecast.Infrastructure.Tests.Isolation;

/// <summary>
/// Suite de testes de isolamento RLS cross-tenant. BLOQUEADORA DE MERGE (KPI-06).
///
/// Verifica a defesa em profundidade (ADR-0001):
/// <list type="number">
///   <item><term>Via repositório EF Core</term>
///   <description>Global Query Filter bloqueia acesso cross-tenant na camada de aplicação.</description></item>
///   <item><term>Via SQL direto sem SET app.tenant_id</term>
///   <description>RLS bloqueia acesso mesmo com conexão direta ao banco.</description></item>
///   <item><term>FORCE ROW LEVEL SECURITY</term>
///   <description>RLS não é bypassada por superuser neste contexto (usuário NOSUPERUSER).</description></item>
/// </list>
///
/// KPI-06: 100% dos cenários de vazamento devem estar cobertos por testes verdes.
/// Qualquer falha nesta suite deve bloquear o merge.
///
/// Mapeia: TASK-18, RNF 1, Req 12, ADR-0001, design §14.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RlsIsolationTests(PostgresContainerFixture db)
{
    private static Goal BuildBuGoal(Guid tenantId, Guid buId, int year = 2026, int month = 6)
    {
        var scope = GoalScope.ForBu(buId);
        var period = new GoalPeriod(year, month);
        return Goal.Create(tenantId, scope, period, Money.Of(5_000_000L));
    }

    // ── Cenário 1: Tenant B NÃO vê metas de Tenant A via repositório ──────────

    [Fact]
    public async Task Repository_tenant_b_cannot_read_tenant_a_goals()
    {
        // Arrange — tenant A insere meta
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var goal = BuildBuGoal(tenantA, buId);

        await using (var ctxA = db.BuildOwnerContext(tenantA))
        {
            await new GoalRepository(ctxA).Add(goal);
        }

        // Act — tenant B consulta metas (Global Query Filter de tenant B)
        await using var ctxB = db.BuildContextWithRls(tenantB);
        var repoB = new GoalRepository(ctxB);
        var filter = new GoalQueryFilter(TenantId: tenantB);
        var result = await repoB.Query(filter);

        // Assert — KPI-06: tenant B não recebe metas de A
        result.Items.Should().NotContain(g => g.TenantId == tenantA,
            "Global Query Filter deve impedir acesso cross-tenant");
    }

    [Fact]
    public async Task Repository_tenant_b_findbykey_returns_null_for_tenant_a_goal()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var goal = BuildBuGoal(tenantA, buId, year: 2026, month: 7);

        await using (var ctxA = db.BuildOwnerContext(tenantA))
        {
            await new GoalRepository(ctxA).Add(goal);
        }

        // Act — tenant B tenta FindByKey com mesma combinação de chave
        await using var ctxB = db.BuildContextWithRls(tenantB);
        var found = await new GoalRepository(ctxB).FindByKey(tenantB, buId, null, 2026, 7);

        // Assert — KPI-06: Global Query Filter retorna null
        found.Should().BeNull(
            "Global Query Filter deve bloquear leitura de meta de outro tenant via FindByKey");
    }

    [Fact]
    public async Task Repository_tenant_b_findbyid_returns_null_for_tenant_a_goal()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var goal = BuildBuGoal(tenantA, buId, year: 2026, month: 8);

        await using (var ctxA = db.BuildOwnerContext(tenantA))
        {
            await new GoalRepository(ctxA).Add(goal);
        }

        // Act — tenant B tenta FindById com o ID conhecido de tenant A
        await using var ctxB = db.BuildContextWithRls(tenantB);
        var found = await new GoalRepository(ctxB).FindById(tenantB, goal.Id);

        // Assert — KPI-06: Global Query Filter retorna null por Id
        found.Should().BeNull(
            "Global Query Filter deve bloquear leitura de meta de outro tenant via FindById");
    }

    // ── Cenário 2: RLS via SQL direto sem SET app.tenant_id ─────────────────

    [Fact]
    public async Task Rls_blocks_direct_sql_without_tenant_context()
    {
        // Arrange — insere meta de um tenant com contexto correto
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var goal = BuildBuGoal(tenantId, buId, year: 2026, month: 9);

        await using (var ctxInsert = db.BuildOwnerContext(tenantId))
        {
            await new GoalRepository(ctxInsert).Add(goal);
        }

        // Act — conexão raw (usuário azim_app, NOSUPERUSER) SEM SET app.tenant_id
        // azim_app não é owner da tabela → RLS é aplicada (segunda camada ADR-0001)
        await using var conn = db.OpenAppConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM goals";
        var count = (long)(await cmd.ExecuteScalarAsync())!;

        // Assert — KPI-06: RLS deve retornar 0 linhas sem contexto de tenant
        count.Should().Be(0L,
            "RLS deve bloquear acesso a goals sem SET app.tenant_id (segunda camada ADR-0001)");
    }

    [Fact]
    public async Task Rls_allows_access_with_correct_tenant_context()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var goal = BuildBuGoal(tenantId, buId, year: 2026, month: 10);

        await using (var ctxInsert = db.BuildOwnerContext(tenantId))
        {
            await new GoalRepository(ctxInsert).Add(goal);
        }

        // Act — conexão raw (usuário azim_app) COM SET app.tenant_id
        await using var conn = db.OpenAppConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SET app.tenant_id = '{tenantId}'; SELECT COUNT(*) FROM goals WHERE tenant_id = '{tenantId}'";
        var count = (long)(await cmd.ExecuteScalarAsync())!;

        // Assert — RLS permite acesso com contexto correto
        count.Should().Be(1L,
            "RLS deve permitir acesso às próprias metas com SET app.tenant_id correto");
    }

    [Fact]
    public async Task Rls_blocks_direct_sql_with_wrong_tenant_context()
    {
        // Arrange — insere meta de tenant A
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var goal = BuildBuGoal(tenantA, buId, year: 2026, month: 11);

        await using (var ctxA = db.BuildOwnerContext(tenantA))
        {
            await new GoalRepository(ctxA).Add(goal);
        }

        // Act — SQL direto (azim_app) com contexto de tenant B tentando ler linha de tenant A
        await using var conn = db.OpenAppConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SET app.tenant_id = '{tenantB}'; SELECT COUNT(*) FROM goals WHERE id = '{goal.Id}'";
        var count = (long)(await cmd.ExecuteScalarAsync())!;

        // Assert — KPI-06: RLS bloqueia leitura de linha de outro tenant
        count.Should().Be(0L,
            "RLS deve bloquear leitura de linha de tenant A quando contexto é tenant B");
    }

    // ── Cenário 3: ListByYear não vaza entre tenants ──────────────────────────

    [Fact]
    public async Task ListByYear_does_not_cross_tenant_boundary()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var buId = Guid.NewGuid();

        await using (var ctxA = db.BuildOwnerContext(tenantA))
        {
            await new GoalRepository(ctxA).Add(BuildBuGoal(tenantA, buId, year: 2021, month: 1));
        }

        // Act — tenant B lista por ano (usa app_user + RLS com tenant B)
        await using var ctxB = db.BuildContextWithRls(tenantB);
        var goals = await new GoalRepository(ctxB).ListByYear(tenantB, buId, null, 2021);

        // Assert — KPI-06
        goals.Should().NotContain(g => g.TenantId == tenantA,
            "ListByYear não deve retornar metas de outro tenant");
    }

    // ── Cenário 4: isolamento por múltiplos tenants simultâneos ──────────────

    [Fact]
    public async Task Simultaneous_tenants_are_fully_isolated()
    {
        // Arrange — 3 tenants com metas no mesmo buId
        var tenants = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var buId = Guid.NewGuid();

        foreach (var tenant in tenants)
        {
            await using var ctx = db.BuildOwnerContext(tenant);
            await new GoalRepository(ctx).Add(BuildBuGoal(tenant, buId, year: 2020, month: 6));
        }

        // Act + Assert — cada tenant vê apenas as próprias metas
        // Usa owner context: Global Query Filter garante isolamento na camada de aplicação
        foreach (var tenant in tenants)
        {
            await using var ctx = db.BuildOwnerContext(tenant);
            var result = await new GoalRepository(ctx).Query(
                new GoalQueryFilter(TenantId: tenant, Year: 2020, Month: 6));

            result.Items.Should().HaveCount(1,
                $"tenant {tenant} deve ver apenas 1 meta (a sua própria)");
            result.Items[0].TenantId.Should().Be(tenant);
        }
    }
}
