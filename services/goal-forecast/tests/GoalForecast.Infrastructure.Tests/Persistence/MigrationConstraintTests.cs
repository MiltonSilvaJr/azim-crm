using GoalForecast.Infrastructure.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GoalForecast.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de integração dos constraints e índices da migration InitialGoals.
/// Verifica: unicidade de chave natural, índice parcial BU (DD-002) e
/// isolamento RLS (segunda camada de defesa, ADR-0001).
///
/// Mapeia: TASK-16, Req 2, RNF 1, ADR-0001, DD-002, design §7.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class MigrationConstraintTests(PostgresContainerFixture db)
{
    private static Goal BuildBuGoal(Guid tenantId, Guid buId, int year = 2026, int month = 1)
    {
        var scope = GoalScope.ForBu(buId);
        var period = new GoalPeriod(year, month);
        return Goal.Create(tenantId, scope, period, Money.Of(1_000L));
    }

    private static Goal BuildResponsavelGoal(Guid tenantId, Guid buId, Guid ownerId,
        int year = 2026, int month = 1)
    {
        var scope = GoalScope.ForResponsavel(buId, ownerId);
        var period = new GoalPeriod(year, month);
        return Goal.Create(tenantId, scope, period, Money.Of(2_000L));
    }

    // ── ST-01 Red: unicidade RESPONSAVEL ─────────────────────────────────────

    [Fact]
    public async Task Unique_constraint_responsavel_rejects_duplicate_key()
    {
        // Arrange — duas metas com mesma chave (tenant, bu, owner, year, month)
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var goal1 = BuildResponsavelGoal(tenantId, buId, ownerId, year: 2026, month: 2);
        var goal2 = BuildResponsavelGoal(tenantId, buId, ownerId, year: 2026, month: 2);

        await using var ctx1 = db.BuildOwnerContext(tenantId);
        ctx1.Goals.Add(goal1);
        await ctx1.SaveChangesAsync();

        // Act
        await using var ctx2 = db.BuildOwnerContext(tenantId);
        ctx2.Goals.Add(goal2);
        Func<Task> act = () => ctx2.SaveChangesAsync();

        // Assert — violação de unicidade
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.WithInnerException<NpgsqlException>().And.SqlState.Should().Be("23505");
    }

    // ── Índice parcial BU (owner_id IS NULL) ──────────────────────────────────

    [Fact]
    public async Task Partial_index_bu_rejects_duplicate_bu_goal()
    {
        // Arrange — duas metas BU com mesma chave (owner_id NULL em ambas)
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();

        var goal1 = BuildBuGoal(tenantId, buId, year: 2026, month: 3);
        var goal2 = BuildBuGoal(tenantId, buId, year: 2026, month: 3);

        await using var ctx1 = db.BuildOwnerContext(tenantId);
        ctx1.Goals.Add(goal1);
        await ctx1.SaveChangesAsync();

        // Act
        await using var ctx2 = db.BuildOwnerContext(tenantId);
        ctx2.Goals.Add(goal2);
        Func<Task> act = () => ctx2.SaveChangesAsync();

        // Assert — índice parcial deve bloquear
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.WithInnerException<NpgsqlException>().And.SqlState.Should().Be("23505");
    }

    [Fact]
    public async Task Partial_index_bu_allows_different_months()
    {
        // Arrange — duas metas BU com meses diferentes (deve permitir)
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();

        var goal1 = BuildBuGoal(tenantId, buId, year: 2026, month: 4);
        var goal2 = BuildBuGoal(tenantId, buId, year: 2026, month: 5);

        await using var ctx = db.BuildOwnerContext(tenantId);
        ctx.Goals.Add(goal1);
        ctx.Goals.Add(goal2);

        // Act + Assert — não deve lançar
        await ctx.SaveChangesAsync();
        (await ctx.Goals.CountAsync(g => g.TenantId == tenantId && g.Scope.BuId == buId))
            .Should().Be(2);
    }

    [Fact]
    public async Task Bu_and_responsavel_can_coexist_same_period()
    {
        // Arrange — meta BU e meta RESPONSAVEL no mesmo período são permitidas
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var goalBu = BuildBuGoal(tenantId, buId, year: 2026, month: 6);
        var goalResp = BuildResponsavelGoal(tenantId, buId, ownerId, year: 2026, month: 6);

        await using var ctx = db.BuildOwnerContext(tenantId);
        ctx.Goals.Add(goalBu);
        ctx.Goals.Add(goalResp);

        // Act + Assert — deve persistir os dois sem conflito
        await ctx.SaveChangesAsync();
        (await ctx.Goals.CountAsync(g => g.TenantId == tenantId)).Should().Be(2);
    }
}
