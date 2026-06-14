using GoalForecast.Infrastructure.Persistence;
using GoalForecast.Infrastructure.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace GoalForecast.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de integração do GoalRepository contra banco PostgreSQL real.
/// Verifica: FindByKey, FindById, Add, Update, Query, ListByYear, Global Query Filter.
/// Mapeia: TASK-17, Req 1, Req 2, Req 3, RNF 1, design §6.1.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class GoalRepositoryTests(PostgresContainerFixture db)
{
    private static Goal BuildBuGoal(Guid tenantId, Guid buId, int year = 2026, int month = 6,
        long cents = 5_000_000L)
    {
        var scope = GoalScope.ForBu(buId);
        var period = new GoalPeriod(year, month);
        return Goal.Create(tenantId, scope, period, Money.Of(cents));
    }

    private static Goal BuildResponsavelGoal(Guid tenantId, Guid buId, Guid ownerId,
        int year = 2026, int month = 6, long cents = 3_000_000L)
    {
        var scope = GoalScope.ForResponsavel(buId, ownerId);
        var period = new GoalPeriod(year, month);
        return Goal.Create(tenantId, scope, period, Money.Of(cents));
    }

    private GoalRepository BuildRepo(GoalForecastDbContext ctx) => new(ctx);

    /// <summary>Constrói contexto owner (sem RLS ativa para owner) para inserções de dados de teste.</summary>
    private GoalForecastDbContext OwnerCtx(Guid tenantId) => db.BuildOwnerContext(tenantId);

    // ── ST-01: FindByKey retorna null para chave inexistente ──────────────────

    [Fact]
    public async Task FindByKey_returns_null_for_nonexistent_key()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = OwnerCtx(tenantId);
        var repo = BuildRepo(ctx);

        var result = await repo.FindByKey(tenantId, Guid.NewGuid(), null, 2026, 7);

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindByKey_returns_goal_for_existing_bu_key()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var goal = BuildBuGoal(tenantId, buId, year: 2026, month: 8);

        await using (var ctxInsert = OwnerCtx(tenantId))
        {
            var repo = BuildRepo(ctxInsert);
            await repo.Add(goal);
        }

        // Act
        await using var ctx = OwnerCtx(tenantId);
        var found = await BuildRepo(ctx).FindByKey(tenantId, buId, null, 2026, 8);

        // Assert
        found.Should().NotBeNull();
        found!.TenantId.Should().Be(tenantId);
        found.Scope.BuId.Should().Be(buId);
        found.Period.Year.Should().Be(2026);
        found.Period.Month.Should().Be(8);
    }

    [Fact]
    public async Task FindByKey_does_not_return_other_tenant_goals()
    {
        // Arrange — tenant A insere meta
        var tenantA = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var goal = BuildBuGoal(tenantA, buId, year: 2026, month: 9);

        await using (var ctxA = db.BuildContextWithRls(tenantA))
        {
            await BuildRepo(ctxA).Add(goal);
        }

        // Act — tenant B tenta encontrar pela chave
        var tenantB = Guid.NewGuid();
        await using var ctxB = OwnerCtx(tenantB);
        var found = await BuildRepo(ctxB).FindByKey(tenantB, buId, null, 2026, 9);

        // Assert — Global Query Filter retorna null
        found.Should().BeNull();
    }

    // ── Add + FindById preserva valor_meta exato ───────────────────────────────

    [Fact]
    public async Task Add_and_FindById_preserves_valor_meta_long()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        const long cents = 12_345_678_901L;
        var goal = BuildBuGoal(tenantId, buId, year: 2026, month: 10, cents: cents);

        await using (var ctxInsert = OwnerCtx(tenantId))
        {
            await BuildRepo(ctxInsert).Add(goal);
        }

        // Act
        await using var ctx = OwnerCtx(tenantId);
        var found = await BuildRepo(ctx).FindById(tenantId, goal.Id);

        // Assert — PBT-05: long preservado exatamente
        found.Should().NotBeNull();
        found!.ValorMeta.Cents.Should().Be(cents);
    }

    // ── Update altera valor_meta e updated_at ────────────────────────────────

    [Fact]
    public async Task Update_changes_valor_meta_and_updates_timestamp()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var goal = BuildBuGoal(tenantId, buId, year: 2026, month: 11, cents: 1_000L);

        await using (var ctxInsert = OwnerCtx(tenantId))
        {
            await BuildRepo(ctxInsert).Add(goal);
        }

        var createdAt = goal.CreatedAt;
        await Task.Delay(50); // garante delta de timestamp

        // Act — usa owner context para Update (não requer RLS para este teste de repositório)
        await using (var ctxUpdate = OwnerCtx(tenantId))
        {
            var loaded = await BuildRepo(ctxUpdate).FindById(tenantId, goal.Id);
            loaded!.ChangeValorMeta(Money.Of(2_000L));
            await BuildRepo(ctxUpdate).Update(loaded);
        }

        // Assert
        await using var ctx = OwnerCtx(tenantId);
        var updated = await BuildRepo(ctx).FindById(tenantId, goal.Id);

        updated!.ValorMeta.Cents.Should().Be(2_000L);
        updated.UpdatedAt.Should().BeAfter(createdAt);
    }

    // ── Query com filtro de bu_id retorna apenas metas da BU ─────────────────

    [Fact]
    public async Task Query_filters_by_buId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buA = Guid.NewGuid();
        var buB = Guid.NewGuid();

        var goalA = BuildBuGoal(tenantId, buA, year: 2025, month: 1);
        var goalB = BuildBuGoal(tenantId, buB, year: 2025, month: 1);

        await using (var ctxInsert = OwnerCtx(tenantId))
        {
            var repo = BuildRepo(ctxInsert);
            await repo.Add(goalA);
            await repo.Add(goalB);
        }

        // Act
        await using var ctx = OwnerCtx(tenantId);
        var filter = new GoalQueryFilter(TenantId: tenantId, BuId: buA);
        var result = await BuildRepo(ctx).Query(filter);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Scope.BuId.Should().Be(buA);
    }

    [Fact]
    public async Task Query_always_filters_by_tenant_via_global_query_filter()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var buId = Guid.NewGuid();

        await using (var ctxA = db.BuildContextWithRls(tenantA))
        {
            await BuildRepo(ctxA).Add(BuildBuGoal(tenantA, buId, year: 2024, month: 6));
        }

        // Act — consulta como tenant B
        await using var ctx = db.BuildContextWithRls(tenantB);
        var result = await BuildRepo(ctx).Query(new GoalQueryFilter(TenantId: tenantB));

        // Assert — não vê metas de A
        result.Items.Should().NotContain(g => g.TenantId == tenantA);
    }

    // ── ListByYear retorna todas as metas do ano ──────────────────────────────

    [Fact]
    public async Task ListByYear_returns_all_months_for_bu()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();

        await using (var ctxInsert = OwnerCtx(tenantId))
        {
            var repo = BuildRepo(ctxInsert);
            for (var m = 1; m <= 6; m++)
                await repo.Add(BuildBuGoal(tenantId, buId, year: 2023, month: m));
        }

        // Act
        await using var ctx = OwnerCtx(tenantId);
        var goals = await BuildRepo(ctx).ListByYear(tenantId, buId, null, 2023);

        // Assert
        goals.Should().HaveCount(6);
        goals.Should().AllSatisfy(g => g.Period.Year.Should().Be(2023));
    }

    // ── Paginação funciona ─────────────────────────────────────────────────────

    [Fact]
    public async Task Query_pagination_returns_correct_page()
    {
        // Arrange — 3 metas em tenant isolado
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();

        await using (var ctxInsert = OwnerCtx(tenantId))
        {
            var repo = BuildRepo(ctxInsert);
            for (var m = 1; m <= 3; m++)
                await repo.Add(BuildBuGoal(tenantId, buId, year: 2022, month: m));
        }

        // Act — página 1 com pageSize 2
        await using var ctx = OwnerCtx(tenantId);
        var page1 = await BuildRepo(ctx).Query(
            new GoalQueryFilter(TenantId: tenantId, Year: 2022, Page: 1, PageSize: 2));

        var page2 = await BuildRepo(ctx).Query(
            new GoalQueryFilter(TenantId: tenantId, Year: 2022, Page: 2, PageSize: 2));

        // Assert
        page1.Items.Should().HaveCount(2);
        page1.Total.Should().Be(3);
        page2.Items.Should().HaveCount(1);
    }
}
