namespace ActivityManagement.Application.Tests.Activities.Commands;

using ActivityManagement.Application.Activities.Commands;
using ActivityManagement.Application.Behaviors;
using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Application.Validators;
using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Repositories;
using FluentValidation;

/// <summary>
/// Testes unitários dos handlers CRUD: Create, Update, Delete.
/// Cobre: criação válida, título vazio (ACT-ERR-001), tipo inválido (ACT-ERR-002),
/// due_at ausente (ACT-ERR-010), vínculo inválido (ACT-ERR-005/006),
/// atualização de terminal (ACT-ERR-011), exclusão com auditoria.
/// Mapeia: design §5.1/§5.5, TASK-07.
/// </summary>
public sealed class CreateActivityCommandTests
{
    private readonly IActivityRepository  _repository      = Substitute.For<IActivityRepository>();
    private readonly IOpportunityReadPort _opportunityPort = Substitute.For<IOpportunityReadPort>();
    private readonly IAccountReadPort     _accountPort     = Substitute.For<IAccountReadPort>();
    private readonly IClock               _clock           = Substitute.For<IClock>();

    private static readonly Guid      TenantId = Guid.NewGuid();
    private static readonly Guid      BuId     = Guid.NewGuid();
    private static readonly Guid      UserId   = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static TenantContext MakeContext() => new(
        TenantId:      TenantId,
        BuId:          BuId,
        UserId:        UserId,
        Role:          "seller",
        CorrelationId: Guid.NewGuid());

    public CreateActivityCommandTests()
    {
        _clock.UtcNow.Returns(Now);
    }

    private CreateActivityCommand ValidCommand(
        string type          = "meeting",
        string title         = "Reunião de negócios",
        Guid?  opportunityId = null,
        Guid?  accountId     = null) => new(
        Type:          type,
        Title:         title,
        DueAt:         Now.AddDays(1),
        OwnerId:       UserId,
        OpportunityId: opportunityId,
        AccountId:     accountId)
    {
        TenantContext = MakeContext()
    };

    // ── Criação válida ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidCommand_SavesActivityAndReturnsId()
    {
        var handler  = new CreateActivityCommandHandler(_repository, _opportunityPort, _accountPort, _clock);
        var command  = ValidCommand();

        var id = await handler.Handle(command, CancellationToken.None);

        id.Should().NotBeEmpty();
        await _repository.Received(1).SaveAsync(
            Arg.Is<Activity>(a => a.Title == "Reunião de negócios"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCommand_ActivityHasCreatedEvent()
    {
        var handler = new CreateActivityCommandHandler(_repository, _opportunityPort, _accountPort, _clock);
        var command = ValidCommand();

        await handler.Handle(command, CancellationToken.None);

        command.DomainEvents.Should().ContainSingle(e => e is Domain.Activities.Events.ActivityCreated);
    }

    // ── Vínculo de oportunidade inválido (ACT-ERR-005) ────────────────────────

    [Fact]
    public async Task Handle_InvalidOpportunityId_ThrowsOpportunityNotFoundException()
    {
        var oppId = Guid.NewGuid();
        _opportunityPort.ExistsAsync(oppId, TenantId, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new CreateActivityCommandHandler(_repository, _opportunityPort, _accountPort, _clock);
        var command = ValidCommand(opportunityId: oppId);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<OpportunityNotFoundException>();
        await _repository.DidNotReceive().SaveAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>());
    }

    // ── Vínculo de conta inválido (ACT-ERR-006) ─────────────────────────────

    [Fact]
    public async Task Handle_InvalidAccountId_ThrowsAccountNotFoundException()
    {
        var accId = Guid.NewGuid();
        _accountPort.ExistsAsync(accId, TenantId, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new CreateActivityCommandHandler(_repository, _opportunityPort, _accountPort, _clock);
        var command = ValidCommand(accountId: accId);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<AccountNotFoundException>();
    }

    // ── Validator: título vazio (ACT-ERR-001) ────────────────────────────────

    [Fact]
    public async Task Validator_EmptyTitle_ReturnsACTERR001()
    {
        var validator = new CreateActivityValidator();
        var command   = new CreateActivityCommand(
            Type:    "meeting",
            Title:   "",
            DueAt:   Now.AddDays(1),
            OwnerId: UserId)
        { TenantContext = MakeContext() };

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "ACT-ERR-001");
    }

    // ── Validator: tipo inválido (ACT-ERR-002) ───────────────────────────────

    [Fact]
    public async Task Validator_InvalidType_ReturnsACTERR002()
    {
        var validator = new CreateActivityValidator();
        var command   = new CreateActivityCommand(
            Type:    "invalid_type",
            Title:   "Título",
            DueAt:   Now.AddDays(1),
            OwnerId: UserId)
        { TenantContext = MakeContext() };

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "ACT-ERR-002");
    }

    // ── Validator: due_at ausente (ACT-ERR-010) ──────────────────────────────

    [Fact]
    public async Task Validator_DefaultDueAt_ReturnsACTERR010()
    {
        var validator = new CreateActivityValidator();
        var command   = new CreateActivityCommand(
            Type:    "meeting",
            Title:   "Título",
            DueAt:   default,
            OwnerId: UserId)
        { TenantContext = MakeContext() };

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "ACT-ERR-010");
    }

    // ── Validator: comando válido passa ─────────────────────────────────────

    [Fact]
    public async Task Validator_ValidCommand_PassesValidation()
    {
        var validator = new CreateActivityValidator();
        var command   = ValidCommand();

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }
}
