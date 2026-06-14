namespace ActivityManagement.Application.Tests.Activities.Queries;

using ActivityManagement.Application.Activities.Commands;
using ActivityManagement.Application.Activities.Queries;
using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Events;
using ActivityManagement.Domain.Activities.Repositories;
using ActivityManagement.Domain.Activities.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Testes unitários das queries de sistema:
/// GetOverdueByUserQuery, GetTodayByUserQuery, GetLastCompletedActivityQuery
/// e ScanOverdueActivitiesCommand (deduplicação por activityId+scanDate).
/// Mapeia: design §5.1/§5.2, Req 11, Req 12, Req 14, DD-005, RNF 4, TASK-12.
/// </summary>
public sealed class GetOverdueByUserQueryTests
{
    private readonly IActivityRepository _repository = Substitute.For<IActivityRepository>();

    private static readonly DateTimeOffset Now      = DateTimeOffset.UtcNow;
    private static readonly Guid           TenantId = Guid.NewGuid();
    private static readonly Guid           UserId   = Guid.NewGuid();

    private static TenantContext MakeContext() => new(TenantId, Guid.NewGuid(), UserId, "seller", Guid.NewGuid());

    [Fact]
    public async Task GetOverdueByUser_ReturnsOnlyNonTerminalOverdue()
    {
        var overdue = Activity.Create(TenantId, Guid.NewGuid(), UserId,
            ActivityType.Create("call"), "Ligação vencida",
            DueDate.Create(Now.AddDays(-2)), Now);

        var completed = Activity.Create(TenantId, Guid.NewGuid(), UserId,
            ActivityType.Create("task"), "Tarefa concluída",
            DueDate.Create(Now.AddDays(-3)), Now);
        completed.Complete(Now);

        _repository.GetOverdueByOwnerAsync(UserId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([overdue, completed]);

        var handler = new GetOverdueByUserQueryHandler(_repository);
        var query   = new GetOverdueByUserQuery(UserId) { TenantContext = MakeContext() };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(overdue.Id);
    }
}

/// <summary>
/// Testes do GetTodayByUserQuery.
/// </summary>
public sealed class GetTodayByUserQueryTests
{
    private readonly IActivityRepository _repository  = Substitute.For<IActivityRepository>();
    private readonly ITenantClock        _tenantClock = Substitute.For<ITenantClock>();

    private static readonly DateTimeOffset Now      = DateTimeOffset.UtcNow;
    private static readonly Guid           TenantId = Guid.NewGuid();
    private static readonly Guid           UserId   = Guid.NewGuid();

    private static TenantContext MakeContext() => new(TenantId, Guid.NewGuid(), UserId, "seller", Guid.NewGuid());

    [Fact]
    public async Task GetTodayByUser_ReturnsNonTerminalTodayActivities()
    {
        var today = Activity.Create(TenantId, Guid.NewGuid(), UserId,
            ActivityType.Create("email"), "E-mail de hoje",
            DueDate.Create(Now), Now);

        var startOfDay = Now.Date;
        var endOfDay   = startOfDay.AddDays(1);

        _tenantClock.GetStartOfDayForTenant(TenantId).Returns(startOfDay);
        _repository.GetByOwnerInRangeAsync(UserId, startOfDay, endOfDay, Arg.Any<CancellationToken>())
            .Returns([today]);

        var handler = new GetTodayByUserQueryHandler(_repository, _tenantClock);
        var query   = new GetTodayByUserQuery(UserId) { TenantContext = MakeContext() };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().ContainSingle(a => a.Id == today.Id);
    }
}

/// <summary>
/// Testes do GetLastCompletedActivityQuery (lote, sem N+1 — RNF 4).
/// </summary>
public sealed class GetLastCompletedActivityQueryTests
{
    private readonly IActivityRepository _repository = Substitute.For<IActivityRepository>();

    private static readonly DateTimeOffset Now      = DateTimeOffset.UtcNow;
    private static readonly Guid           TenantId = Guid.NewGuid();

    private static TenantContext MakeContext() => new(TenantId, Guid.NewGuid(), Guid.NewGuid(), "seller", Guid.NewGuid());

    [Fact]
    public async Task GetLastCompleted_CallsRepositoryOnce_NoPlusN()
    {
        var oppId1 = Guid.NewGuid();
        var oppId2 = Guid.NewGuid();

        var activity1 = Activity.Create(TenantId, Guid.NewGuid(), Guid.NewGuid(),
            ActivityType.Create("meeting"), "Reunião", DueDate.Create(Now.AddDays(-1)), Now);
        activity1.Complete(Now.AddDays(-1));

        _repository.GetLastCompletedByOpportunitiesAsync(
                Arg.Any<IReadOnlyList<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, Activity> { [oppId1] = activity1 });

        var handler = new GetLastCompletedActivityQueryHandler(_repository);
        var query   = new GetLastCompletedActivityQuery([oppId1, oppId2]) { TenantContext = MakeContext() };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().ContainKey(oppId1);
        result.Should().NotContainKey(oppId2);

        // Verifica que o repositório foi chamado uma única vez (sem N+1)
        await _repository.Received(1).GetLastCompletedByOpportunitiesAsync(
            Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<CancellationToken>());
    }
}

/// <summary>
/// Testes do ScanOverdueActivitiesCommand com deduplicação (activityId, scanDate) — DD-005.
/// </summary>
public sealed class ScanOverdueActivitiesCommandTests
{
    private readonly IActivityRepository _repository     = Substitute.For<IActivityRepository>();
    private readonly IOutboxPublisher    _outbox         = Substitute.For<IOutboxPublisher>();
    private readonly IClock              _clock          = Substitute.For<IClock>();
    private readonly IActivityMetrics    _metrics        = Substitute.For<IActivityMetrics>();

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public ScanOverdueActivitiesCommandTests()
    {
        _clock.UtcNow.Returns(Now);
    }

    private Activity CreateOverdueActivity() =>
        Activity.Create(
            tenantId: Guid.NewGuid(),
            buId:     Guid.NewGuid(),
            ownerId:  Guid.NewGuid(),
            type:     ActivityType.Create("follow_up"),
            title:    "Follow-up vencido",
            dueAt:    DueDate.Create(Now.AddDays(-1)),
            now:      Now.AddDays(-2));

    // ── Scan básico: atividades vencidas recebem ActivityOverdue ────────────

    [Fact]
    public async Task Handle_OverdueActivities_EnqueuesActivityOverduePerActivity()
    {
        var a1 = CreateOverdueActivity();
        var a2 = CreateOverdueActivity();

        // Primeira chamada (skip=0) retorna 2 atividades; demais, vazia
        var callCount = 0;
        _repository.GetOverduePageAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return callCount == 1
                    ? Task.FromResult<IReadOnlyList<Activity>>([a1, a2])
                    : Task.FromResult<IReadOnlyList<Activity>>([]);
            });

        var handler = new ScanOverdueActivitiesCommandHandler(_repository, _outbox, _clock, _metrics, NullLogger<ScanOverdueActivitiesCommandHandler>.Instance);
        var command = new ScanOverdueActivitiesCommand();

        await handler.Handle(command, CancellationToken.None);

        await _outbox.Received(2).EnqueueAsync(
            Arg.Any<ActivityOverdue>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    // ── Deduplicação: chave = (activityId, scanDate) — DD-005 ───────────────

    [Fact]
    public async Task Handle_EnqueuesWithDeduplicationKey_ContainingActivityIdAndScanDate()
    {
        var activity  = CreateOverdueActivity();
        var callCount = 0;
        _repository.GetOverduePageAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return callCount == 1
                    ? Task.FromResult<IReadOnlyList<Activity>>([activity])
                    : Task.FromResult<IReadOnlyList<Activity>>([]);
            });

        var handler = new ScanOverdueActivitiesCommandHandler(_repository, _outbox, _clock, _metrics, NullLogger<ScanOverdueActivitiesCommandHandler>.Instance);
        var command = new ScanOverdueActivitiesCommand();

        await handler.Handle(command, CancellationToken.None);

        var expectedScanDate = DateOnly.FromDateTime(Now.UtcDateTime);
        var expectedKey      = $"{activity.Id}:{expectedScanDate:yyyy-MM-dd}";

        await _outbox.Received(1).EnqueueAsync(
            Arg.Any<ActivityOverdue>(),
            Arg.Is<string?>(k => k == expectedKey),
            Arg.Any<CancellationToken>());
    }

    // ── Scan não altera status das atividades — DD-005 ─────────────────────

    [Fact]
    public async Task Handle_DoesNotAlterActivityStatus()
    {
        var activity      = CreateOverdueActivity();
        var initialStatus = activity.Status;
        var callCount     = 0;

        _repository.GetOverduePageAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return callCount == 1
                    ? Task.FromResult<IReadOnlyList<Activity>>([activity])
                    : Task.FromResult<IReadOnlyList<Activity>>([]);
            });

        var handler = new ScanOverdueActivitiesCommandHandler(_repository, _outbox, _clock, _metrics, NullLogger<ScanOverdueActivitiesCommandHandler>.Instance);
        var command = new ScanOverdueActivitiesCommand();

        await handler.Handle(command, CancellationToken.None);

        activity.Status.Should().Be(initialStatus, "scan não deve alterar o status de nenhuma atividade (DD-005)");
        await _repository.DidNotReceive().SaveAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>());
    }

    // ── Nenhuma atividade vencida → sem enfileiramento ──────────────────────

    [Fact]
    public async Task Handle_NoOverdueActivities_DoesNotEnqueue()
    {
        _repository.GetOverduePageAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = new ScanOverdueActivitiesCommandHandler(_repository, _outbox, _clock, _metrics, NullLogger<ScanOverdueActivitiesCommandHandler>.Instance);
        var command = new ScanOverdueActivitiesCommand();

        await handler.Handle(command, CancellationToken.None);

        await _outbox.DidNotReceive().EnqueueAsync(
            Arg.Any<ActivityOverdue>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }
}
