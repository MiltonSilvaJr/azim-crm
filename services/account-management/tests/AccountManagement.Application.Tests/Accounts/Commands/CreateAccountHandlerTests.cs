using AccountManagement.Application.Accounts.Commands.CreateAccount;
using AccountManagement.Application.Behaviors;
using AccountManagement.Application.Exceptions;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Events;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace AccountManagement.Application.Tests.Accounts.Commands;

/// <summary>
/// Testes unitários para <see cref="CreateAccountHandler"/>.
///
/// Mapeia: TASK-04 ST-01, design §5.1, Req 1, DD-006, ADR-0009.
/// </summary>
public sealed class CreateAccountHandlerTests
{
    private readonly IAccountRepository _repository;
    private readonly IEventPublisher _publisher;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _buId = Guid.NewGuid();

    public CreateAccountHandlerTests()
    {
        _repository = Substitute.For<IAccountRepository>();
        _publisher = Substitute.For<IEventPublisher>();
    }

    private CreateAccountHandler BuildHandler(BuScopeContext? buScopeContext = null)
    {
        buScopeContext ??= BuildSingleBuScope(_buId);
        return new CreateAccountHandler(_repository, _publisher, buScopeContext);
    }

    private static BuScopeContext BuildSingleBuScope(Guid buId)
    {
        var ctx = new BuScopeContext();
        ctx.SetScope(new[] { buId }, isTenantWide: false);
        return ctx;
    }

    private static BuScopeContext BuildMultiBuScope(params Guid[] buIds)
    {
        var ctx = new BuScopeContext();
        ctx.SetScope(buIds, isTenantWide: false);
        return ctx;
    }

    private static BuScopeContext BuildTenantWideScope()
    {
        var ctx = new BuScopeContext();
        ctx.SetScope(Array.Empty<Guid>(), isTenantWide: true);
        return ctx;
    }

    [Fact(DisplayName = "CreateAccount: usuário com 1 BU — conta criada automaticamente nessa BU")]
    public async Task Handle_SingleBuUser_CreatesAccountInThatBu()
    {
        // Arrange
        var handler = BuildHandler(BuildSingleBuScope(_buId));
        var command = new CreateAccountCommand(
            TenantId: _tenantId,
            Name: "Pag.ai Tecnologia",
            Website: "https://pag.ai",
            Notes: null,
            BuId: null,
            ConfirmCreateDespiteSimilar: false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        await _repository.Received(1).SaveAsync(
            Arg.Is<Account>(a => a.TenantId == _tenantId
                                 && a.BuId == _buId
                                 && a.Name.Value == "Pag.ai Tecnologia"),
            Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AccountCreated>(e => e.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CreateAccount: usuário multi-BU sem bu_id no request → ACC-ERR-010")]
    public async Task Handle_MultiBuUserWithoutBuId_ThrowsBuIdRequired()
    {
        var bu1 = Guid.NewGuid();
        var bu2 = Guid.NewGuid();
        var handler = BuildHandler(BuildMultiBuScope(bu1, bu2));
        var command = new CreateAccountCommand(
            TenantId: _tenantId, Name: "Empresa", Website: null, Notes: null,
            BuId: null, ConfirmCreateDespiteSimilar: false);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<BuIdRequiredForMultiBuUserException>();
    }

    [Fact(DisplayName = "CreateAccount: usuário multi-BU com bu_id fora do escopo → ACC-ERR-011")]
    public async Task Handle_MultiBuUserWithBuIdOutOfScope_ThrowsBuNotInScope()
    {
        var bu1 = Guid.NewGuid();
        var bu2 = Guid.NewGuid();
        var outsideBu = Guid.NewGuid();
        var handler = BuildHandler(BuildMultiBuScope(bu1, bu2));
        var command = new CreateAccountCommand(
            TenantId: _tenantId, Name: "Empresa", Website: null, Notes: null,
            BuId: outsideBu, ConfirmCreateDespiteSimilar: false);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<BuNotInScopeException>()
            .Where(e => e.RequestedBuId == outsideBu);
    }

    [Fact(DisplayName = "CreateAccount: usuário multi-BU com bu_id válido → conta criada na BU informada")]
    public async Task Handle_MultiBuUserWithValidBuId_CreatesAccountInSpecifiedBu()
    {
        var bu1 = Guid.NewGuid();
        var bu2 = Guid.NewGuid();
        var handler = BuildHandler(BuildMultiBuScope(bu1, bu2));
        var command = new CreateAccountCommand(
            TenantId: _tenantId, Name: "Empresa", Website: null, Notes: null,
            BuId: bu2, ConfirmCreateDespiteSimilar: false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        await _repository.Received(1).SaveAsync(
            Arg.Is<Account>(a => a.BuId == bu2),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CreateAccount: tenant-wide requer bu_id (sem resolução automática)")]
    public async Task Handle_TenantWideWithoutBuId_ThrowsBuIdRequired()
    {
        var handler = BuildHandler(BuildTenantWideScope());
        var command = new CreateAccountCommand(
            TenantId: _tenantId, Name: "Empresa", Website: null, Notes: null,
            BuId: null, ConfirmCreateDespiteSimilar: false);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<BuIdRequiredForMultiBuUserException>();
    }

    [Fact(DisplayName = "CreateAccount: tenant-wide com bu_id — aceita qualquer BU do tenant")]
    public async Task Handle_TenantWideWithBuId_CreatesAccountInSpecifiedBu()
    {
        var anyBu = Guid.NewGuid();
        var handler = BuildHandler(BuildTenantWideScope());
        var command = new CreateAccountCommand(
            TenantId: _tenantId, Name: "Empresa", Website: null, Notes: null,
            BuId: anyBu, ConfirmCreateDespiteSimilar: false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        await _repository.Received(1).SaveAsync(
            Arg.Is<Account>(a => a.BuId == anyBu),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CreateAccount: com similar existente e confirmCreateDespiteSimilar=true — conta é criada")]
    public async Task Handle_WithSimilarAndConfirmFlag_CreatesAccount()
    {
        var handler = BuildHandler();
        var command = new CreateAccountCommand(
            TenantId: _tenantId, Name: "Pag AI", Website: null, Notes: null,
            BuId: null, ConfirmCreateDespiteSimilar: true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        await _repository.Received(1).SaveAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CreateAccount: NormalizedName é calculado corretamente no domínio")]
    public async Task Handle_ValidCommand_NormalizedNameIsComputed()
    {
        Account? savedAccount = null;
        await _repository.SaveAsync(
            Arg.Do<Account>(a => savedAccount = a),
            Arg.Any<CancellationToken>());

        var handler = BuildHandler();
        var command = new CreateAccountCommand(
            TenantId: _tenantId, Name: "Pág.AI Tecnologia", Website: null, Notes: null,
            BuId: null, ConfirmCreateDespiteSimilar: false);

        await handler.Handle(command, CancellationToken.None);

        savedAccount.Should().NotBeNull();
        savedAccount!.NormalizedName.Value.Should().Be("pagai tecnologia");
    }

    [Fact(DisplayName = "CreateAccount: AccountCreated event NormalizedName sem PII")]
    public async Task Handle_ValidCommand_EventContainsNoPii()
    {
        AccountCreated? publishedEvent = null;
        await _publisher.PublishAsync(
            Arg.Do<AccountCreated>(e => publishedEvent = e),
            Arg.Any<CancellationToken>());

        var handler = BuildHandler();
        var command = new CreateAccountCommand(
            TenantId: _tenantId, Name: "Empresa Teste", Website: null, Notes: null,
            BuId: null, ConfirmCreateDespiteSimilar: false);

        await handler.Handle(command, CancellationToken.None);

        publishedEvent.Should().NotBeNull();
        publishedEvent!.NormalizedName.Should().Be("empresa teste");
        publishedEvent.TenantId.Should().Be(_tenantId);
    }
}
