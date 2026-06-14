namespace ActivityManagement.Application.Tests.Activities.Commands;

using ActivityManagement.Application.Activities.Commands;
using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Exceptions;
using ActivityManagement.Domain.Activities.Repositories;
using ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Testes unitários dos handlers UpdateActivity e DeleteActivity.
/// Cobre: update em terminal (ACT-ERR-011), exclusão com auditoria, not found (ACT-ERR-003).
/// Mapeia: design §5.1, Req 2, TASK-07.
/// </summary>
public sealed class UpdateDeleteActivityCommandTests
{
    private readonly IActivityRepository  _repository      = Substitute.For<IActivityRepository>();
    private readonly IOpportunityReadPort _opportunityPort = Substitute.For<IOpportunityReadPort>();
    private readonly IAccountReadPort     _accountPort     = Substitute.For<IAccountReadPort>();
    private readonly IClock               _clock           = Substitute.For<IClock>();
    private readonly IAuditPublisher      _auditPublisher  = Substitute.For<IAuditPublisher>();

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static TenantContext MakeContext(string role = "seller") => new(
        TenantId:      Guid.NewGuid(),
        BuId:          Guid.NewGuid(),
        UserId:        Guid.NewGuid(),
        Role:          role,
        CorrelationId: Guid.NewGuid());

    private static Activity CreateActivity(string status = "pending")
    {
        var activity = Activity.Create(
            tenantId:        Guid.NewGuid(),
            buId:            Guid.NewGuid(),
            ownerId:         Guid.NewGuid(),
            type:            ActivityType.Create("meeting"),
            title:           "Atividade de teste",
            dueAt:           DueDate.Create(Now.AddDays(1)),
            now:             Now);

        if (status == "completed")
            activity.Complete(Now);
        else if (status == "cancelled")
            activity.Cancel(Now);

        return activity;
    }

    public UpdateDeleteActivityCommandTests()
    {
        _clock.UtcNow.Returns(Now);
    }

    // ── UpdateActivity: atividade terminal retorna ACT-ERR-011 ──────────────

    [Fact]
    public async Task UpdateActivity_OnCompletedActivity_ThrowsActivityTerminalException()
    {
        var activity = CreateActivity("completed");
        _repository.FindByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        var handler = new UpdateActivityCommandHandler(_repository, _opportunityPort, _accountPort, _clock);
        var command = new UpdateActivityCommand(
            ActivityId: activity.Id,
            Title:      "Novo título",
            DueAt:      Now.AddDays(2))
        { TenantContext = MakeContext() };

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ActivityTerminalException>();
        await _repository.DidNotReceive().SaveAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>());
    }

    // ── UpdateActivity: atividade não encontrada (ACT-ERR-003) ──────────────

    [Fact]
    public async Task UpdateActivity_NotFound_ThrowsActivityNotFoundException()
    {
        _repository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Activity?)null);

        var handler = new UpdateActivityCommandHandler(_repository, _opportunityPort, _accountPort, _clock);
        var command = new UpdateActivityCommand(
            ActivityId: Guid.NewGuid(),
            Title:      "Título",
            DueAt:      Now.AddDays(1))
        { TenantContext = MakeContext() };

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ActivityNotFoundException>();
    }

    // ── UpdateActivity: atualização válida persiste ──────────────────────────

    [Fact]
    public async Task UpdateActivity_ValidCommand_SavesActivity()
    {
        var activity = CreateActivity();
        _repository.FindByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        var handler = new UpdateActivityCommandHandler(_repository, _opportunityPort, _accountPort, _clock);
        var command = new UpdateActivityCommand(
            ActivityId: activity.Id,
            Title:      "Título atualizado",
            DueAt:      Now.AddDays(3))
        { TenantContext = MakeContext() };

        await handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).SaveAsync(
            Arg.Is<Activity>(a => a.Title == "Título atualizado"),
            Arg.Any<CancellationToken>());
    }

    // ── DeleteActivity: registra auditoria ──────────────────────────────────

    [Fact]
    public async Task DeleteActivity_ValidId_DeletesAndPublishesAudit()
    {
        var activity = CreateActivity();
        _repository.FindByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        var handler = new DeleteActivityCommandHandler(_repository, _auditPublisher);
        var command = new DeleteActivityCommand(activity.Id)
        { TenantContext = MakeContext() };

        await handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).DeleteAsync(activity.Id, Arg.Any<CancellationToken>());
        await _auditPublisher.Received(1).PublishAsync(
            Arg.Is<AuditEntry>(a => a.Action == "deleted" && a.EntityId == activity.Id),
            Arg.Any<CancellationToken>());
    }

    // ── DeleteActivity: atividade não encontrada (ACT-ERR-003) ──────────────

    [Fact]
    public async Task DeleteActivity_NotFound_ThrowsActivityNotFoundException()
    {
        _repository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Activity?)null);

        var handler = new DeleteActivityCommandHandler(_repository, _auditPublisher);
        var command = new DeleteActivityCommand(Guid.NewGuid())
        { TenantContext = MakeContext() };

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ActivityNotFoundException>();
        await _auditPublisher.DidNotReceive().PublishAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }
}
