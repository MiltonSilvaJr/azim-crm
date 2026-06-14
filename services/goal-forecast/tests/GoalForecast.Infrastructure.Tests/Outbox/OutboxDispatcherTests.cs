using System.Text.Json;
using GoalForecast.Domain.Events;
using GoalForecast.Infrastructure.Outbox;
using GoalForecast.Infrastructure.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GoalForecast.Infrastructure.Tests.Outbox;

/// <summary>
/// Testes de integração do OutboxDispatcher contra PostgreSQL real.
/// Verifica: evento gravado atomicamente com o Goal; rollback reverte ambos;
/// serialização preserva <c>long</c> sem conversão para <c>double</c> (PBT-05 serialização).
///
/// Mapeia: TASK-20, Req 10, RNF 5, design §6.6, PBT-05.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OutboxDispatcherTests(PostgresContainerFixture db)
{
    // ── ST-01 Red ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispatch_persists_event_in_outbox_events_table()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        const long valorMeta = 123_456_789L;

        await using var ctx = db.BuildOwnerContext(tenantId);
        var dispatcher = new OutboxDispatcher(ctx);

        var goalEvent = BuildGoalUpdatedEvent(tenantId, buId, valorMeta);

        // Act
        await dispatcher.DispatchAsync(goalEvent);
        await ctx.Database.ExecuteSqlAsync($"SELECT 1"); // força flush da transação implícita

        // Assert — verifica presença do evento no banco
        var count = await CountOutboxEvents(goalEvent.EventId, tenantId);
        count.Should().Be(1L, "evento deve ser gravado em outbox_events");
    }

    [Fact]
    public async Task Dispatch_preserves_long_without_double_conversion()
    {
        // Arrange — valor que NÃO pode ser representado exatamente como double
        // (double tem 53 bits de mantissa; long tem 63 bits de valor)
        const long largeLong = 9_007_199_254_740_993L; // 2^53 + 1: não representável como double

        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();

        await using var ctx = db.BuildOwnerContext(tenantId);
        var dispatcher = new OutboxDispatcher(ctx);

        var goalEvent = BuildGoalUpdatedEvent(tenantId, buId, largeLong);

        // Act
        await dispatcher.DispatchAsync(goalEvent);

        // Assert — lê o payload JSONB e verifica que o long foi preservado exatamente
        var payload = await ReadOutboxPayload(goalEvent.EventId);
        payload.Should().NotBeNull();

        using var doc = JsonDocument.Parse(payload!);
        var valorMetaNovo = doc.RootElement.GetProperty("valorMetaNovo").GetInt64();

        valorMetaNovo.Should().Be(largeLong,
            "serialização deve preservar long sem conversão para double (PBT-05)");
    }

    [Fact]
    public async Task Add_goal_with_outbox_persists_both_atomically()
    {
        // Arrange — insere Goal + GoalUpdated no mesmo SaveChangesAsync
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        const long valorMeta = 50_000L;

        await using var ctx = db.BuildOwnerContext(tenantId);
        var dispatcher = new OutboxDispatcher(ctx);
        var repo = new GoalRepository(ctx, dispatcher);

        var scope = GoalScope.ForBu(buId);
        var period = new GoalPeriod(2026, 1);
        var goal = Goal.Create(tenantId, scope, period, Money.Of(valorMeta));

        // Act
        await repo.Add(goal);

        // Assert — ambos persistidos
        var savedGoal = await new GoalRepository(db.BuildOwnerContext(tenantId))
            .FindById(tenantId, goal.Id);
        savedGoal.Should().NotBeNull("goal deve ser persistido");
        savedGoal!.ValorMeta.Cents.Should().Be(valorMeta);

        var eventCount = await CountOutboxEventsByTenant(tenantId);
        eventCount.Should().BeGreaterThanOrEqualTo(1L, "ao menos 1 evento deve estar no outbox");
    }

    [Fact]
    public async Task Rollback_reverts_both_goal_and_outbox_event()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        const long valorMeta = 99_000L;

        await using var ctx = db.BuildOwnerContext(tenantId);
        var dispatcher = new OutboxDispatcher(ctx);

        var scope = GoalScope.ForBu(buId);
        var period = new GoalPeriod(2026, 2);
        var goal = Goal.Create(tenantId, scope, period, Money.Of(valorMeta));

        // Act — inicia transação, insere goal + evento, depois faz rollback
        await using var tx = await ctx.Database.BeginTransactionAsync();
        ctx.Goals.Add(goal);
        var goalEvent = BuildGoalUpdatedEvent(tenantId, buId, valorMeta);
        await dispatcher.DispatchAsync(goalEvent);
        await tx.RollbackAsync(); // reverte tudo

        // Assert — nem o goal nem o evento devem existir
        var goalCount = await CountGoals(goal.Id);
        goalCount.Should().Be(0L, "rollback deve reverter inserção do goal");

        var eventCount = await CountOutboxEvents(goalEvent.EventId, tenantId);
        eventCount.Should().Be(0L, "rollback deve reverter inserção do evento no outbox");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GoalUpdated BuildGoalUpdatedEvent(Guid tenantId, Guid buId, long valorMetaNovo)
    {
        return new GoalUpdated(
            goalId: Guid.NewGuid(),
            tenantId: tenantId,
            buId: buId,
            ownerId: null,
            year: 2026,
            month: 6,
            action: GoalUpdatedAction.Created,
            valorMetaAnterior: null,
            valorMetaNovo: valorMetaNovo,
            occurredAt: DateTimeOffset.UtcNow);
    }

    private async Task<long> CountOutboxEvents(Guid eventId, Guid tenantId)
    {
        await using var conn = new NpgsqlConnection(db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM outbox_events WHERE event_id = @id AND tenant_id = @tenantId";
        cmd.Parameters.AddWithValue("id", eventId);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<long> CountOutboxEventsByTenant(Guid tenantId)
    {
        await using var conn = new NpgsqlConnection(db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM outbox_events WHERE tenant_id = @tenantId";
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<string?> ReadOutboxPayload(Guid eventId)
    {
        await using var conn = new NpgsqlConnection(db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT payload::text FROM outbox_events WHERE event_id = @id";
        cmd.Parameters.AddWithValue("id", eventId);
        return (string?)(await cmd.ExecuteScalarAsync());
    }

    private async Task<long> CountGoals(Guid goalId)
    {
        await using var conn = new NpgsqlConnection(db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM goals WHERE id = @id";
        cmd.Parameters.AddWithValue("id", goalId);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}
