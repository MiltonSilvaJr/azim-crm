namespace ActivityManagement.Application.Tests.Activities.Commands;

using ActivityManagement.Application.Activities.Commands;
using ActivityManagement.Application.Activities.Queries;
using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Events;
using ActivityManagement.Domain.Activities.Exceptions;
using ActivityManagement.Domain.Activities.Repositories;
using ActivityManagement.Domain.Activities.ValueObjects;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

/// <summary>
/// Testes unitários e PBT-02 do CompleteActivityCommand e SuggestNextActivityQuery.
/// PBT-02: N ≥ 1 chamadas de CompleteActivity → exatamente 1 ActivityCompleted acumulado,
///         1 completedAt (o da primeira conclusão efetiva).
/// Mapeia: design §5.1, Req 6, Req 9, RNF 3, DD-004, PBT-02, TASK-08.
/// </summary>
public sealed class CompleteActivityCommandTests
{
    private readonly IActivityRepository _repository     = Substitute.For<IActivityRepository>();
    private readonly IAuditPublisher     _auditPublisher = Substitute.For<IAuditPublisher>();
    private readonly IClock              _clock          = Substitute.For<IClock>();

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public CompleteActivityCommandTests()
    {
        _clock.UtcNow.Returns(Now);
    }

    private static TenantContext MakeContext() => new(
        TenantId:      Guid.NewGuid(),
        BuId:          Guid.NewGuid(),
        UserId:        Guid.NewGuid(),
        Role:          "seller",
        CorrelationId: Guid.NewGuid());

    private static Activity CreatePendingActivity(
        Guid? opportunityId = null,
        Guid? accountId     = null)
    {
        var oppLink = opportunityId.HasValue ? OpportunityLink.Create(opportunityId.Value) : null;
        var accLink = accountId.HasValue     ? AccountLink.Create(accountId.Value)         : null;

        return Activity.Create(
            tenantId:        Guid.NewGuid(),
            buId:            Guid.NewGuid(),
            ownerId:         Guid.NewGuid(),
            type:            ActivityType.Create("meeting"),
            title:           "Reunião de teste",
            dueAt:           DueDate.Create(Now.AddDays(1)),
            now:             Now,
            opportunityLink: oppLink,
            accountLink:     accLink);
    }

    // ── Conclusão válida ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PendingActivity_CompletesAndSavesWithEvent()
    {
        var activity = CreatePendingActivity();
        _repository.FindByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        var handler = new CompleteActivityCommandHandler(_repository, _auditPublisher, _clock);
        var command = new CompleteActivityCommand(activity.Id) { TenantContext = MakeContext() };

        var result = await handler.Handle(command, CancellationToken.None);

        result.WasAlreadyCompleted.Should().BeFalse();
        result.CompletedAt.Should().Be(Now);
        command.DomainEvents.Should().ContainSingle(e => e is ActivityCompleted);
        await _repository.Received(1).SaveAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>());
    }

    // ── Idempotência: já completed → no-op (DD-004) ──────────────────────────

    [Fact]
    public async Task Handle_AlreadyCompleted_ReturnsSuccessWithoutNewEventOrSave()
    {
        var activity = CreatePendingActivity();
        activity.Complete(Now);
        activity.ClearDomainEvents(); // limpa eventos do primeiro Complete

        _repository.FindByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        var handler = new CompleteActivityCommandHandler(_repository, _auditPublisher, _clock);
        var command = new CompleteActivityCommand(activity.Id) { TenantContext = MakeContext() };

        var result = await handler.Handle(command, CancellationToken.None);

        result.WasAlreadyCompleted.Should().BeTrue();
        result.CompletedAt.Should().Be(Now);
        command.DomainEvents.Should().BeEmpty("chamada idempotente não deve acumular novos eventos");
        await _repository.DidNotReceive().SaveAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>());
    }

    // ── Cancelada → ACT-ERR-004 ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_CancelledActivity_ThrowsActivityTerminalException()
    {
        var activity = CreatePendingActivity();
        activity.Cancel(Now);
        _repository.FindByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        var handler = new CompleteActivityCommandHandler(_repository, _auditPublisher, _clock);
        var command = new CompleteActivityCommand(activity.Id) { TenantContext = MakeContext() };

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ActivityTerminalException>();
    }

    // ── Not found → ACT-ERR-003 ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_NotFound_ThrowsActivityNotFoundException()
    {
        _repository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Activity?)null);

        var handler = new CompleteActivityCommandHandler(_repository, _auditPublisher, _clock);
        var command = new CompleteActivityCommand(Guid.NewGuid()) { TenantContext = MakeContext() };

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ActivityNotFoundException>();
    }

    // ── PBT-02: N chamadas → exatamente 1 ActivityCompleted acumulado ────────

    [Property(MaxTest = 100)]
    public Property PBT02_NCompletions_ExactlyOneActivityCompletedAccumulated(FsCheck.PositiveInt n)
    {
        // Arrange
        var activity = CreatePendingActivity();
        var firstNow = Now;

        // Act: chama Complete N vezes no domínio diretamente (testa invariante do domínio)
        for (var i = 0; i < n.Get; i++)
            activity.Complete(firstNow.AddSeconds(i)); // cada chamada com time diferente

        // Assert: exatamente 1 ActivityCompleted acumulado
        var completedEvents = activity.DomainEvents.OfType<ActivityCompleted>().ToList();
        var completedAtIsFirstCall = activity.CompletedAt == firstNow;

        return (completedEvents.Count == 1 && completedAtIsFirstCall).ToProperty();
    }
}

/// <summary>
/// Testes unitários do SuggestNextActivityQuery.
/// Mapeia: design §5.2, Req 9, PBT-05, TASK-08.
/// </summary>
public sealed class SuggestNextActivityQueryTests
{
    private readonly IActivityRepository  _repository      = Substitute.For<IActivityRepository>();
    private readonly IOpportunityReadPort _opportunityPort = Substitute.For<IOpportunityReadPort>();

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static TenantContext MakeContext() => new(
        TenantId:      Guid.NewGuid(),
        BuId:          Guid.NewGuid(),
        UserId:        Guid.NewGuid(),
        Role:          "seller",
        CorrelationId: Guid.NewGuid());

    // ── Oportunidade aberta → retorna sugestão ───────────────────────────────

    [Fact]
    public async Task Handle_ActivityWithOpenOpportunity_ReturnsSuggestion()
    {
        var oppId    = Guid.NewGuid();
        var accId    = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var activity = Activity.Create(
            tenantId:        tenantId,
            buId:            Guid.NewGuid(),
            ownerId:         Guid.NewGuid(),
            type:            ActivityType.Create("follow_up"),
            title:           "Follow-up",
            dueAt:           DueDate.Create(Now.AddDays(1)),
            now:             Now,
            opportunityLink: OpportunityLink.Create(oppId),
            accountLink:     AccountLink.Create(accId));

        var ctx = new TenantContext(tenantId, Guid.NewGuid(), Guid.NewGuid(), "seller", Guid.NewGuid());

        _repository.FindByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        _opportunityPort.IsOpenAsync(oppId, tenantId, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new SuggestNextActivityQueryHandler(_repository, _opportunityPort);
        var query   = new SuggestNextActivityQuery(activity.Id) { TenantContext = ctx };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.OpportunityId.Should().Be(oppId);
        result.AccountId.Should().Be(accId);
    }

    // ── Oportunidade fechada → sem sugestão ─────────────────────────────────

    [Fact]
    public async Task Handle_ActivityWithClosedOpportunity_ReturnsNull()
    {
        var oppId    = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var activity = Activity.Create(
            tenantId:        tenantId,
            buId:            Guid.NewGuid(),
            ownerId:         Guid.NewGuid(),
            type:            ActivityType.Create("meeting"),
            title:           "Reunião",
            dueAt:           DueDate.Create(Now.AddDays(1)),
            now:             Now,
            opportunityLink: OpportunityLink.Create(oppId));

        var ctx = new TenantContext(tenantId, Guid.NewGuid(), Guid.NewGuid(), "seller", Guid.NewGuid());

        _repository.FindByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        _opportunityPort.IsOpenAsync(oppId, tenantId, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new SuggestNextActivityQueryHandler(_repository, _opportunityPort);
        var query   = new SuggestNextActivityQuery(activity.Id) { TenantContext = ctx };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().BeNull("oportunidade fechada não gera sugestão — Req 9.5");
    }

    // ── Sem vínculo de oportunidade → sem sugestão ──────────────────────────

    [Fact]
    public async Task Handle_ActivityWithoutOpportunityLink_ReturnsNull()
    {
        var activity = Activity.Create(
            tenantId:  Guid.NewGuid(),
            buId:      Guid.NewGuid(),
            ownerId:   Guid.NewGuid(),
            type:      ActivityType.Create("task"),
            title:     "Tarefa avulsa",
            dueAt:     DueDate.Create(Now.AddDays(1)),
            now:       Now);

        _repository.FindByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        var handler = new SuggestNextActivityQueryHandler(_repository, _opportunityPort);
        var query   = new SuggestNextActivityQuery(activity.Id) { TenantContext = MakeContext() };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().BeNull("sem vínculo de oportunidade não há sugestão");
    }
}
