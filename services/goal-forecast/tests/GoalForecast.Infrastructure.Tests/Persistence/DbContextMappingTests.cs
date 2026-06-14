using GoalForecast.Infrastructure.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace GoalForecast.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de integração do GoalForecastDbContext: mapeamentos EF Core,
/// preservação de Money como BIGINT e Global Query Filter por tenant_id.
/// Mapeia: TASK-15, design §6.1, RNF 4.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class DbContextMappingTests(PostgresContainerFixture db)
{
    private static Goal BuildGoal(Guid tenantId, Guid buId, int year = 2026, int month = 6,
        long cents = 5_000_000L)
    {
        var scope = GoalScope.ForBu(buId);
        var period = new GoalPeriod(year, month);
        var money = Money.Of(cents);
        return Goal.Create(tenantId, scope, period, money);
    }

    // ── ST-01 Red: valor_meta preservado como BIGINT (long exato) ────────────

    [Fact]
    public async Task Add_and_find_preserves_valor_meta_exact_long()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        const long cents = 9_999_999_999L; // valor grande que representaria perda em double
        var goal = BuildGoal(tenantId, buId, cents: cents);

        await using var ctx = db.BuildContextWithRls(tenantId);

        // Act
        ctx.Goals.Add(goal);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var loaded = await ctx.Goals.FirstOrDefaultAsync(g => g.Id == goal.Id);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.ValorMeta.Cents.Should().Be(cents);
    }

    [Fact]
    public async Task Global_query_filter_blocks_different_tenant()
    {
        // Arrange — tenant A insere meta
        var tenantA = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var goal = BuildGoal(tenantA, buId);

        await using (var ctxA = db.BuildContextWithRls(tenantA))
        {
            ctxA.Goals.Add(goal);
            await ctxA.SaveChangesAsync();
        }

        // Act — tenant B tenta ver as metas de A
        var tenantB = Guid.NewGuid();
        await using var ctxB = db.BuildContextWithRls(tenantB);
        var results = await ctxB.Goals.Where(g => g.Id == goal.Id).ToListAsync();

        // Assert — Global Query Filter deve retornar vazio
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Global_query_filter_allows_same_tenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var goal = BuildGoal(tenantId, buId);

        await using (var ctxInsert = db.BuildContextWithRls(tenantId))
        {
            ctxInsert.Goals.Add(goal);
            await ctxInsert.SaveChangesAsync();
        }

        // Act
        await using var ctxRead = db.BuildContextWithRls(tenantId);
        var loaded = await ctxRead.Goals.FirstOrDefaultAsync(g => g.Id == goal.Id);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task GoalPeriod_year_and_month_preserved()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var goal = BuildGoal(tenantId, buId, year: 2025, month: 11);

        await using var ctx = db.BuildContextWithRls(tenantId);
        ctx.Goals.Add(goal);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // Act
        var loaded = await ctx.Goals.FirstAsync(g => g.Id == goal.Id);

        // Assert
        loaded.Period.Year.Should().Be(2025);
        loaded.Period.Month.Should().Be(11);
    }

    [Fact]
    public async Task GoalScope_buId_not_null_preserved()
    {
        // Arrange — bu_id é NOT NULL (DD-008)
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var goal = BuildGoal(tenantId, buId);

        await using var ctx = db.BuildContextWithRls(tenantId);
        ctx.Goals.Add(goal);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // Act
        var loaded = await ctx.Goals.FirstAsync(g => g.Id == goal.Id);

        // Assert
        loaded.Scope.BuId.Should().Be(buId);
        loaded.Scope.OwnerId.Should().BeNull();
    }

    [Fact]
    public async Task GoalScope_ownerId_preserved_when_responsavel()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var scope = GoalScope.ForResponsavel(buId, ownerId);
        var period = new GoalPeriod(2026, 3);
        var goal = Goal.Create(tenantId, scope, period, Money.Of(1000L));

        await using var ctx = db.BuildContextWithRls(tenantId);
        ctx.Goals.Add(goal);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // Act
        var loaded = await ctx.Goals.FirstAsync(g => g.Id == goal.Id);

        // Assert
        loaded.Scope.BuId.Should().Be(buId);
        loaded.Scope.OwnerId.Should().Be(ownerId);
        loaded.Scope.Kind.Should().Be(GoalScopeKind.RESPONSAVEL);
    }
}
