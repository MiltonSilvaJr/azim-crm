using AccountManagement.Application.Accounts.Commands.UpdateAccount;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Events;
using AccountManagement.Domain.Accounts.Exceptions;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace AccountManagement.Application.Tests.Accounts.Commands;

/// <summary>
/// Testes unitários para <see cref="UpdateAccountHandler"/>.
///
/// Mapeia: TASK-04 ST-01, design §5.1, Req 4.2, ACC-ERR-001, ACC-ERR-003.
/// </summary>
public sealed class UpdateAccountHandlerTests
{
    private readonly IAccountRepository _repository;
    private readonly IEventPublisher _publisher;
    private readonly UpdateAccountHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public UpdateAccountHandlerTests()
    {
        _repository = Substitute.For<IAccountRepository>();
        _publisher = Substitute.For<IEventPublisher>();
        _handler = new UpdateAccountHandler(_repository, _publisher);
    }

    private Account BuildAccount(string name = "Conta Original")
    {
        var normalizer = new NameNormalizer();
        return Account.Create(
            _tenantId,
            Guid.NewGuid(),
            AccountName.Create(name),
            website: null,
            notes: null,
            normalizer);
    }

    [Fact(DisplayName = "UpdateAccount: renomeia conta e NormalizedName é recalculado")]
    public async Task Handle_ValidCommand_RenamesAccountAndRecalculatesNormalizedName()
    {
        // Arrange
        var account = BuildAccount();
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        var command = new UpdateAccountCommand(
            AccountId: account.Id,
            Name: "Pág AI Tecnologia",
            Website: null,
            Notes: null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        account.Name.Value.Should().Be("Pág AI Tecnologia");
        account.NormalizedName.Value.Should().Be("pag ai tecnologia");
    }

    [Fact(DisplayName = "UpdateAccount: evento AccountUpdated é publicado após renomear")]
    public async Task Handle_ValidCommand_PublishesAccountUpdatedEvent()
    {
        // Arrange
        var account = BuildAccount();
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        var command = new UpdateAccountCommand(
            AccountId: account.Id,
            Name: "Novo Nome",
            Website: null,
            Notes: null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AccountUpdated>(e => e.AccountId == account.Id && e.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "UpdateAccount: conta não encontrada lança AccountNotFoundException (ACC-ERR-003)")]
    public async Task Handle_AccountNotFound_ThrowsAccountNotFoundException()
    {
        // Arrange
        var unknownId = Guid.NewGuid();
        _repository.GetByIdAsync(unknownId, Arg.Any<CancellationToken>())
            .Returns((Account?)null);

        var command = new UpdateAccountCommand(
            AccountId: unknownId,
            Name: "Qualquer Nome",
            Website: null,
            Notes: null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AccountNotFoundException>();
    }
}
