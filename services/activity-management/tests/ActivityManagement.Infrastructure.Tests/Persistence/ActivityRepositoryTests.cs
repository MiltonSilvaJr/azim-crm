namespace ActivityManagement.Infrastructure.Tests.Persistence;

using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Repositories;
using ActivityManagement.Infrastructure.Persistence.Repositories;
using ActivityManagement.Domain.Activities.ValueObjects;
using ActivityManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

/// <summary>
/// Testes de integração do <see cref="ActivityRepository"/> contra PostgreSQL real (Testcontainers).
/// Valida: round-trip de persistência, nomes de coluna snake_case,
/// métodos de consulta (overdue, range, list, last-completed).
/// Mapeia: TASK-13, design §6.1, design §7.
/// </summary>
public sealed class ActivityRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .WithDatabase("activity_test")
        .Build();

    private ActivityManagementDbContext _context = null!;
    private ActivityRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<ActivityManagementDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new ActivityManagementDbContext(options);
        _context.SetTenant(TenantA);

        // Aplica migrations via SQL direto (sem dotnet ef no teste)
        await _context.Database.ExecuteSqlRawAsync(InitialSchemaSql);

        _repository = new ActivityRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Activity CreateActivity(
        Guid?   tenantId    = null,
        Guid?   ownerId     = null,
        string  title       = "Reunião inicial",
        string  type        = "meeting",
        DateTimeOffset? dueAt = null) =>
        Activity.Create(
            tenantId:  tenantId ?? TenantA,
            buId:      Guid.NewGuid(),
            ownerId:   ownerId ?? Guid.NewGuid(),
            type:      ActivityType.Create(type),
            title:     title,
            dueAt:     DueDate.Create(dueAt ?? Now.AddDays(1)),
            now:       Now);

    // ── Testes de persistência básica ──────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_Then_FindByIdAsync_Returns_Same_Activity()
    {
        // Arrange
        var activity = CreateActivity();

        // Act
        await _repository.SaveAsync(activity);
        var retrieved = await _repository.FindByIdAsync(activity.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(activity.Id);
        retrieved.TenantId.Should().Be(activity.TenantId);
        retrieved.Title.Should().Be(activity.Title);
        retrieved.Type.Should().Be(activity.Type);
        retrieved.Status.Should().Be(ActivityStatus.Pending);
        retrieved.Priority.Should().Be(Priority.Default);
        retrieved.DueAt.Should().Be(activity.DueAt);
    }

    [Fact]
    public async Task FindByIdAsync_Returns_Null_When_Not_Found()
    {
        // Act
        var result = await _repository.FindByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_Removes_Activity()
    {
        // Arrange
        var activity = CreateActivity();
        await _repository.SaveAsync(activity);

        // Act
        await _repository.DeleteAsync(activity.Id);
        var retrieved = await _repository.FindByIdAsync(activity.Id);

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task Column_Names_Are_Snake_Case()
    {
        // Arrange
        var activity = CreateActivity();
        await _repository.SaveAsync(activity);

        // Act: verificar via SQL direto que colunas existem com os nomes corretos
        await using var conn = new Npgsql.NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT column_name FROM information_schema.columns
            WHERE table_name = 'activities'
            ORDER BY column_name";

        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));

        // Assert: colunas obrigatórias em snake_case
        columns.Should().Contain("id");
        columns.Should().Contain("tenant_id");
        columns.Should().Contain("bu_id");
        columns.Should().Contain("owner_id");
        columns.Should().Contain("activity_type");
        columns.Should().Contain("title");
        columns.Should().Contain("due_at");
        columns.Should().Contain("status");
        columns.Should().Contain("priority");
        columns.Should().Contain("completed_at");
        columns.Should().Contain("created_at");
        columns.Should().Contain("updated_at");
    }

    [Fact]
    public async Task GetOverduePageAsync_Returns_Only_Non_Terminal_Overdue()
    {
        // Arrange
        var overdueActivity = CreateActivity(dueAt: Now.AddDays(-2)); // vencida
        var futureActivity = CreateActivity(dueAt: Now.AddDays(2));   // futura
        await _repository.SaveAsync(overdueActivity);
        await _repository.SaveAsync(futureActivity);

        // Act
        var result = await _repository.GetOverduePageAsync(Now, skip: 0, take: 10);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(overdueActivity.Id);
    }

    [Fact]
    public async Task GetOverduePageAsync_Does_Not_Return_Terminal_Activities()
    {
        // Arrange
        var activity = CreateActivity(dueAt: Now.AddDays(-2));
        activity.Complete(Now);
        await _repository.SaveAsync(activity);

        // Act
        var result = await _repository.GetOverduePageAsync(Now, skip: 0, take: 10);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByOwnerInRangeAsync_Returns_Activities_In_Date_Range()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var inRange = CreateActivity(ownerId: ownerId, dueAt: Now.AddHours(2));
        var outRange = CreateActivity(ownerId: ownerId, dueAt: Now.AddDays(5));
        await _repository.SaveAsync(inRange);
        await _repository.SaveAsync(outRange);

        // Act
        var result = await _repository.GetByOwnerInRangeAsync(
            ownerId, from: Now, to: Now.AddDays(1));

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(inRange.Id);
    }

    [Fact]
    public async Task GetLastCompletedByOpportunitiesAsync_Returns_Most_Recent_Completed()
    {
        // Arrange
        var oppId = Guid.NewGuid();
        var link = OpportunityLink.Create(oppId);
        var older = Activity.Create(TenantA, Guid.NewGuid(), Guid.NewGuid(),
            ActivityType.Create("call"), "Chamada antiga",
            DueDate.Create(Now.AddDays(-5)), Now.AddDays(-5), opportunityLink: link);
        older.Complete(Now.AddDays(-5));

        var newer = Activity.Create(TenantA, Guid.NewGuid(), Guid.NewGuid(),
            ActivityType.Create("meeting"), "Reunião recente",
            DueDate.Create(Now.AddDays(-1)), Now.AddDays(-1), opportunityLink: link);
        newer.Complete(Now.AddDays(-1));

        await _repository.SaveAsync(older);
        await _repository.SaveAsync(newer);

        // Act
        var result = await _repository.GetLastCompletedByOpportunitiesAsync([oppId]);

        // Assert
        result.Should().ContainKey(oppId);
        result[oppId].Id.Should().Be(newer.Id);
    }

    [Fact]
    public async Task ListAsync_Filters_By_Owner()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var mine = CreateActivity(ownerId: ownerId);
        var others = CreateActivity(); // owner diferente
        await _repository.SaveAsync(mine);
        await _repository.SaveAsync(others);

        // Act
        var (items, total) = await _repository.ListAsync(new ActivityListFilter(OwnerId: ownerId));

        // Assert
        total.Should().Be(1);
        items.Should().HaveCount(1);
        items[0].OwnerId.Should().Be(ownerId);
    }

    [Fact]
    public async Task GetOverdueByOwnerAsync_Returns_Only_Overdue_For_Owner()
    {
        // Arrange
        var ownerId   = Guid.NewGuid();
        var overdue   = CreateActivity(ownerId: ownerId, dueAt: Now.AddDays(-2));
        var notOverdue = CreateActivity(ownerId: ownerId, dueAt: Now.AddDays(2));
        var otherOwner = CreateActivity(dueAt: Now.AddDays(-2)); // owner diferente
        await _repository.SaveAsync(overdue);
        await _repository.SaveAsync(notOverdue);
        await _repository.SaveAsync(otherOwner);

        // Act
        var result = await _repository.GetOverdueByOwnerAsync(ownerId, Now);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(overdue.Id);
    }

    [Fact]
    public async Task GetOpportunityIdsWithoutFollowupAsync_Returns_Ids_Without_Active_Activities()
    {
        // Arrange: oportunidade sem atividade ativa
        var buId         = Guid.NewGuid();
        var oppWithout   = Guid.NewGuid();
        var oppWithActive = Guid.NewGuid();

        // Atividade completada (terminal) → opp sem follow-up futuro
        var completed = Activity.Create(TenantA, buId, Guid.NewGuid(),
            ActivityType.Create("call"), "Concluída",
            DueDate.Create(Now.AddDays(-1)), Now.AddDays(-1),
            opportunityLink: OpportunityLink.Create(oppWithout));
        completed.Complete(Now.AddDays(-1));

        // Atividade pendente com due_at futura → opp com follow-up ativo
        var active = Activity.Create(TenantA, buId, Guid.NewGuid(),
            ActivityType.Create("meeting"), "Ativa",
            DueDate.Create(Now.AddDays(2)), Now,
            opportunityLink: OpportunityLink.Create(oppWithActive));

        await _repository.SaveAsync(completed);
        await _repository.SaveAsync(active);

        var allOppIds = new Guid[] { oppWithout, oppWithActive };

        // Act
        var result = await _repository.GetOpportunityIdsWithoutFollowupAsync(
            buId, allOppIds, referenceInstant: Now);

        // Assert: apenas a oportunidade sem atividade ativa futura
        result.Should().Contain(oppWithout,
            because: "oportunidade com apenas atividade concluída não tem follow-up ativo futuro");
        result.Should().NotContain(oppWithActive,
            because: "oportunidade com atividade pendente futura tem follow-up ativo");
    }

    // ── SQL do schema inicial (sem migrations EF, para simplicidade nos testes) ──

    private const string InitialSchemaSql = @"
        CREATE TABLE IF NOT EXISTS activities (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id       UUID NOT NULL,
            bu_id           UUID NOT NULL,
            owner_id        UUID NOT NULL,
            opportunity_id  UUID,
            account_id      UUID,
            activity_type   VARCHAR(20) NOT NULL,
            title           TEXT NOT NULL,
            description     TEXT,
            due_at          TIMESTAMPTZ NOT NULL,
            status          VARCHAR(20) NOT NULL DEFAULT 'pending',
            priority        VARCHAR(10) NOT NULL DEFAULT 'medium',
            completed_at    TIMESTAMPTZ,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
            CONSTRAINT chk_activities_title_not_blank CHECK (length(btrim(title)) > 0),
            CONSTRAINT chk_activities_type CHECK (activity_type IN ('meeting','follow_up','call','email','task')),
            CONSTRAINT chk_activities_status CHECK (status IN ('pending','in_progress','completed','cancelled')),
            CONSTRAINT chk_activities_priority CHECK (priority IN ('low','medium','high')),
            CONSTRAINT chk_activities_completed_consistency
                CHECK ((status = 'completed') = (completed_at IS NOT NULL))
        );

        CREATE TABLE IF NOT EXISTS digest_action_tokens (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id       UUID NOT NULL,
            user_id         UUID NOT NULL,
            activity_id     UUID REFERENCES activities(id),
            action          VARCHAR(20) NOT NULL,
            token_hash      TEXT NOT NULL,
            expires_at      TIMESTAMPTZ NOT NULL,
            used_at         TIMESTAMPTZ,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
            CONSTRAINT chk_digest_action_type CHECK (action IN ('complete','reschedule'))
        );
        CREATE UNIQUE INDEX IF NOT EXISTS uq_digest_action_tokens_hash ON digest_action_tokens (token_hash);

        CREATE TABLE IF NOT EXISTS outbox_messages (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id       UUID NOT NULL,
            event_type      TEXT NOT NULL,
            dedup_key       TEXT,
            payload_json    TEXT NOT NULL,
            occurred_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
            published_at    TIMESTAMPTZ
        );
        CREATE INDEX IF NOT EXISTS idx_outbox_unpublished ON outbox_messages (published_at) WHERE published_at IS NULL;
        CREATE UNIQUE INDEX IF NOT EXISTS uq_outbox_dedup ON outbox_messages (event_type, dedup_key) WHERE dedup_key IS NOT NULL;

        CREATE TABLE IF NOT EXISTS audit_logs (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id       UUID NOT NULL,
            user_id         UUID,
            entity_type     TEXT NOT NULL,
            entity_id       UUID NOT NULL,
            action          TEXT NOT NULL,
            delta_json      TEXT NOT NULL,
            correlation_id  UUID,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        CREATE INDEX IF NOT EXISTS idx_audit_logs_tenant_entity ON audit_logs (tenant_id, entity_type, entity_id);
    ";
}
