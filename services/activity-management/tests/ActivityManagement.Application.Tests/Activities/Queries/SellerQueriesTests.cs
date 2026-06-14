namespace ActivityManagement.Application.Tests.Activities.Queries;

using ActivityManagement.Application.Activities.Queries;
using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Repositories;
using ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Testes unitários das queries de visão do vendedor:
/// GetMyDayQuery, GetMyWeekQuery, ListActivitiesQuery, GetOpportunitiesWithoutFollowupQuery.
/// Mapeia: design §5.2, Req 5, Req 10, Req 13, DD-008, TASK-11.
/// </summary>
public sealed class GetMyDayQueryTests
{
    private readonly IActivityRepository  _repository     = Substitute.For<IActivityRepository>();
    private readonly IOpportunityReadPort _opportunityPort = Substitute.For<IOpportunityReadPort>();
    private readonly ITenantClock         _tenantClock    = Substitute.For<ITenantClock>();

    private static readonly DateTimeOffset Now      = DateTimeOffset.UtcNow;
    private static readonly Guid           TenantId = Guid.NewGuid();
    private static readonly Guid           UserId   = Guid.NewGuid();
    private static readonly Guid           BuId     = Guid.NewGuid();

    private static TenantContext MakeContext() => new(
        TenantId:      TenantId,
        BuId:          BuId,
        UserId:        UserId,
        Role:          "seller",
        CorrelationId: Guid.NewGuid());

    private Activity CreatePendingActivity(DateTimeOffset dueAt) =>
        Activity.Create(
            tenantId: TenantId,
            buId:     BuId,
            ownerId:  UserId,
            type:     ActivityType.Create("meeting"),
            title:    "Reunião",
            dueAt:    DueDate.Create(dueAt),
            now:      Now);

    // ── GetMyDayQuery: agrupamento em três faixas ────────────────────────────

    [Fact]
    public async Task GetMyDay_ReturnsTodayActivities()
    {
        var todayActivity = CreatePendingActivity(Now);

        _repository.GetOverdueByOwnerAsync(UserId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.GetByOwnerInRangeAsync(UserId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([todayActivity]);
        _tenantClock.GetStartOfDayForTenant(TenantId).Returns(Now.Date);
        _tenantClock.GetStartOfWeekForTenant(TenantId).Returns(Now.Date.AddDays(-(int)Now.DayOfWeek));
        _tenantClock.GetEndOfWeekForTenant(TenantId).Returns(Now.Date.AddDays(7 - (int)Now.DayOfWeek));

        var handler = new GetMyDayQueryHandler(_repository, _tenantClock);
        var query   = new GetMyDayQuery() { TenantContext = MakeContext() };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Today.Should().HaveCount(1);
        result.Overdue.Should().BeEmpty();
        result.Upcoming.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyDay_TerminalActivities_NeverAppearInAnyBand()
    {
        var completedActivity = CreatePendingActivity(Now);
        completedActivity.Complete(Now);

        // Mesmo que o repositório retorne atividades terminais (mock), o handler filtra
        _repository.GetOverdueByOwnerAsync(UserId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([completedActivity]);
        _repository.GetByOwnerInRangeAsync(UserId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([completedActivity]);
        _tenantClock.GetStartOfDayForTenant(TenantId).Returns(Now.Date);
        _tenantClock.GetEndOfWeekForTenant(TenantId).Returns(Now.Date.AddDays(7));

        var handler = new GetMyDayQueryHandler(_repository, _tenantClock);
        var query   = new GetMyDayQuery() { TenantContext = MakeContext() };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Overdue.Should().BeEmpty("atividades terminais nunca aparecem em vencidas");
        result.Today.Should().BeEmpty("atividades terminais nunca aparecem em hoje");
        result.Upcoming.Should().BeEmpty("atividades terminais nunca aparecem em próximas");
    }

    [Fact]
    public async Task GetMyDay_OverdueActivities_AppearInOverdueBand()
    {
        var overdueActivity = CreatePendingActivity(Now.AddDays(-2));

        _repository.GetOverdueByOwnerAsync(UserId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([overdueActivity]);
        _repository.GetByOwnerInRangeAsync(UserId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _tenantClock.GetStartOfDayForTenant(TenantId).Returns(Now.Date);
        _tenantClock.GetEndOfWeekForTenant(TenantId).Returns(Now.Date.AddDays(7));

        var handler = new GetMyDayQueryHandler(_repository, _tenantClock);
        var query   = new GetMyDayQuery() { TenantContext = MakeContext() };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Overdue.Should().HaveCount(1);
        result.Today.Should().BeEmpty();
        result.Upcoming.Should().BeEmpty();
    }
}

/// <summary>
/// Testes do GetMyWeekQuery.
/// Mapeia: design §5.2, Req 5, TASK-11.
/// </summary>
public sealed class GetMyWeekQueryTests
{
    private readonly IActivityRepository _repository  = Substitute.For<IActivityRepository>();
    private readonly ITenantClock        _tenantClock = Substitute.For<ITenantClock>();

    private static readonly DateTimeOffset Now      = DateTimeOffset.UtcNow;
    private static readonly Guid           TenantId = Guid.NewGuid();
    private static readonly Guid           UserId   = Guid.NewGuid();
    private static readonly Guid           BuId     = Guid.NewGuid();

    private static TenantContext MakeContext() => new(TenantId, BuId, UserId, "seller", Guid.NewGuid());

    private Activity CreatePendingActivity(DateTimeOffset dueAt) =>
        Activity.Create(
            tenantId: TenantId,
            buId:     BuId,
            ownerId:  UserId,
            type:     ActivityType.Create("call"),
            title:    "Ligação",
            dueAt:    DueDate.Create(dueAt),
            now:      Now);

    [Fact]
    public async Task GetMyWeek_ReturnsActivitiesWithinWeek()
    {
        var weekActivity = CreatePendingActivity(Now.AddDays(3));
        var weekStart = Now.Date.AddDays(-(int)Now.DayOfWeek);
        var weekEnd   = weekStart.AddDays(7);

        _repository.GetByOwnerInRangeAsync(
                UserId,
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns([weekActivity]);
        _tenantClock.GetStartOfWeekForTenant(TenantId).Returns(weekStart);
        _tenantClock.GetEndOfWeekForTenant(TenantId).Returns(weekEnd);

        var handler = new GetMyWeekQueryHandler(_repository, _tenantClock);
        var query   = new GetMyWeekQuery() { TenantContext = MakeContext() };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMyWeek_TerminalActivities_AreExcluded()
    {
        var completed = CreatePendingActivity(Now.AddDays(2));
        completed.Complete(Now);

        _repository.GetByOwnerInRangeAsync(
                UserId,
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns([completed]);
        _tenantClock.GetStartOfWeekForTenant(TenantId).Returns(Now.Date);
        _tenantClock.GetEndOfWeekForTenant(TenantId).Returns(Now.Date.AddDays(7));

        var handler = new GetMyWeekQueryHandler(_repository, _tenantClock);
        var query   = new GetMyWeekQuery() { TenantContext = MakeContext() };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().BeEmpty("atividades terminais nunca aparecem na visão da semana");
    }
}

/// <summary>
/// Testes do ListActivitiesQuery.
/// Mapeia: design §5.2, Req 13, TASK-11.
/// </summary>
public sealed class ListActivitiesQueryTests
{
    private readonly IActivityRepository _repository = Substitute.For<IActivityRepository>();

    private static readonly DateTimeOffset Now      = DateTimeOffset.UtcNow;
    private static readonly Guid           TenantId = Guid.NewGuid();
    private static readonly Guid           UserId   = Guid.NewGuid();
    private static readonly Guid           BuId     = Guid.NewGuid();

    private static TenantContext MakeContext() => new(TenantId, BuId, UserId, "seller", Guid.NewGuid());

    [Fact]
    public async Task ListActivities_ReturnsPagedResult()
    {
        var activity = Activity.Create(
            tenantId: TenantId, buId: BuId, ownerId: UserId,
            type: ActivityType.Create("task"), title: "Tarefa",
            dueAt: DueDate.Create(Now.AddDays(1)), now: Now);

        _repository.ListAsync(
                Arg.Any<ActivityListFilter>(),
                Arg.Any<CancellationToken>())
            .Returns(([activity], 1));

        var handler = new ListActivitiesQueryHandler(_repository);
        var query   = new ListActivitiesQuery(Page: 1, PageSize: 20) { TenantContext = MakeContext() };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task ListActivities_PassesFiltersToRepository()
    {
        _repository.ListAsync(Arg.Any<ActivityListFilter>(), Arg.Any<CancellationToken>())
            .Returns(([], 0));

        var handler = new ListActivitiesQueryHandler(_repository);
        var query   = new ListActivitiesQuery(
            OwnerId:      UserId,
            Type:         "meeting",
            OnlyOverdue:  true,
            Page:         2,
            PageSize:     10) { TenantContext = MakeContext() };

        await handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).ListAsync(
            Arg.Is<ActivityListFilter>(f =>
                f.OwnerId      == UserId    &&
                f.Type         == "meeting" &&
                f.OnlyOverdue  == true      &&
                f.Page         == 2         &&
                f.PageSize     == 10),
            Arg.Any<CancellationToken>());
    }
}

/// <summary>
/// Testes do GetOpportunitiesWithoutFollowupQuery.
/// Mapeia: design §5.2, Req 10, PBT-05, TASK-11.
/// </summary>
public sealed class GetOpportunitiesWithoutFollowupQueryTests
{
    private readonly IActivityRepository  _repository      = Substitute.For<IActivityRepository>();
    private readonly IOpportunityReadPort _opportunityPort = Substitute.For<IOpportunityReadPort>();

    private static readonly DateTimeOffset Now      = DateTimeOffset.UtcNow;
    private static readonly Guid           TenantId = Guid.NewGuid();
    private static readonly Guid           UserId   = Guid.NewGuid();
    private static readonly Guid           BuId     = Guid.NewGuid();

    private static TenantContext MakeContext() => new(TenantId, BuId, UserId, "bu_manager", Guid.NewGuid());

    [Fact]
    public async Task GetOpportunitiesWithoutFollowup_ReturnsOnlyUnattended()
    {
        var oppWithFollowup    = Guid.NewGuid();
        var oppWithoutFollowup = Guid.NewGuid();
        var openOpportunities  = new List<Guid> { oppWithFollowup, oppWithoutFollowup };

        _opportunityPort.GetOpenOpportunityIdsForBuAsync(BuId, TenantId, Arg.Any<CancellationToken>())
            .Returns(openOpportunities);

        _repository.GetOpportunityIdsWithoutFollowupAsync(
                BuId,
                Arg.Any<IReadOnlyList<Guid>>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns([oppWithoutFollowup]);

        var handler = new GetOpportunitiesWithoutFollowupQueryHandler(_repository, _opportunityPort);
        var query   = new GetOpportunitiesWithoutFollowupQuery() { TenantContext = MakeContext() };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().ContainSingle(id => id == oppWithoutFollowup);
        result.Should().NotContain(oppWithFollowup);
    }

    [Fact]
    public async Task GetOpportunitiesWithoutFollowup_NoOpenOpportunities_ReturnsEmpty()
    {
        _opportunityPort.GetOpenOpportunityIdsForBuAsync(BuId, TenantId, Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = new GetOpportunitiesWithoutFollowupQueryHandler(_repository, _opportunityPort);
        var query   = new GetOpportunitiesWithoutFollowupQuery() { TenantContext = MakeContext() };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().BeEmpty();
        await _repository.DidNotReceive().GetOpportunityIdsWithoutFollowupAsync(
            Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }
}
