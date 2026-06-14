namespace ActivityManagement.Application.Tests.Activities.Commands;

using ActivityManagement.Application.Activities.Commands;
using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Exceptions;
using ActivityManagement.Domain.Activities.Repositories;
using ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Testes unitários do RescheduleActivityCommand.
/// Mapeia: design §5.1, Req 8, Req 8.5, ACT-ERR-011, TASK-10.
/// </summary>
public sealed class RescheduleActivityCommandTests
{
    private readonly IActivityRepository _repository     = Substitute.For<IActivityRepository>();
    private readonly IAuditPublisher     _auditPublisher = Substitute.For<IAuditPublisher>();
    private readonly IClock              _clock          = Substitute.For<IClock>();

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public RescheduleActivityCommandTests()
    {
        _clock.UtcNow.Returns(Now);
    }

    private static TenantContext MakeContext() => new(
        TenantId:      Guid.NewGuid(),
        BuId:          Guid.NewGuid(),
        UserId:        Guid.NewGuid(),
        Role:          "seller",
        CorrelationId: Guid.NewGuid());

    private static Activity CreatePendingActivity() =>
        Activity.Create(
            tenantId: Guid.NewGuid(),
            buId:     Guid.NewGuid(),
            ownerId:  Guid.NewGuid(),
            type:     ActivityType.Create("meeting"),
            title:    "Reunião original",
            dueAt:    DueDate.Create(Now.AddDays(1)),
            now:      Now);

    // ── Reagendamento bem-sucedido ────────────────────────────────────────────

    [Fact]
    public async Task Handle_PendingActivity_ReschedulesAndSavesWithAudit()
    {
        var activity   = CreatePendingActivity();
        var newDueAt   = Now.AddDays(7);
        var ctx        = MakeContext();

        _repository.FindByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        var handler = new RescheduleActivityCommandHandler(_repository, _auditPublisher, _clock);
        var command = new RescheduleActivityCommand(activity.Id, newDueAt) { TenantContext = ctx };

        var result = await handler.Handle(command, CancellationToken.None);

        result.ActivityId.Should().Be(activity.Id);
        result.NewDueAt.Should().Be(newDueAt);
        activity.DueAt.Value.Should().Be(newDueAt);
        await _repository.Received(1).SaveAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>());
        await _auditPublisher.Received(1).PublishAsync(
            Arg.Is<AuditEntry>(e => e.Action == "rescheduled"),
            Arg.Any<CancellationToken>());
    }

    // ── Status inalterado após reagendamento ─────────────────────────────────

    [Fact]
    public async Task Handle_PendingActivity_DoesNotChangeStatus()
    {
        var activity = CreatePendingActivity();
        var ctx      = MakeContext();

        _repository.FindByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        var handler = new RescheduleActivityCommandHandler(_repository, _auditPublisher, _clock);
        var command = new RescheduleActivityCommand(activity.Id, Now.AddDays(5)) { TenantContext = ctx };

        await handler.Handle(command, CancellationToken.None);

        activity.Status.Should().Be(ActivityStatus.Pending, "reagendamento não deve alterar o status");
    }

    // ── Atividade terminal → ACT-ERR-011 ────────────────────────────────────

    [Fact]
    public async Task Handle_CompletedActivity_ThrowsActivityTerminalException()
    {
        var activity = CreatePendingActivity();
        activity.Complete(Now);
        _repository.FindByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        var handler = new RescheduleActivityCommandHandler(_repository, _auditPublisher, _clock);
        var command = new RescheduleActivityCommand(activity.Id, Now.AddDays(7)) { TenantContext = MakeContext() };

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ActivityTerminalException>();
        await _repository.DidNotReceive().SaveAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancelledActivity_ThrowsActivityTerminalException()
    {
        var activity = CreatePendingActivity();
        activity.Cancel(Now);
        _repository.FindByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        var handler = new RescheduleActivityCommandHandler(_repository, _auditPublisher, _clock);
        var command = new RescheduleActivityCommand(activity.Id, Now.AddDays(7)) { TenantContext = MakeContext() };

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ActivityTerminalException>();
        await _repository.DidNotReceive().SaveAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>());
    }

    // ── Não encontrada → ActivityNotFoundException ───────────────────────────

    [Fact]
    public async Task Handle_NotFound_ThrowsActivityNotFoundException()
    {
        _repository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Activity?)null);

        var handler = new RescheduleActivityCommandHandler(_repository, _auditPublisher, _clock);
        var command = new RescheduleActivityCommand(Guid.NewGuid(), Now.AddDays(3)) { TenantContext = MakeContext() };

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ActivityNotFoundException>();
    }
}
