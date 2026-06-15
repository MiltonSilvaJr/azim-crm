using AccountManagement.Application.Contacts.Commands.CreateContact;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Events;
using AccountManagement.Domain.Accounts.Exceptions;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace AccountManagement.Application.Tests.Contacts.Commands;

/// <summary>
/// Testes unitários para <see cref="CreateContactHandler"/>.
///
/// Mapeia: TASK-06 ST-01, design §5.1, Req 5, ACC-ERR-004, ACC-ERR-005, DD-003.
/// </summary>
public sealed class CreateContactHandlerTests
{
    private readonly IAccountRepository _repository;
    private readonly IEventPublisher _publisher;
    private readonly CreateContactHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public CreateContactHandlerTests()
    {
        _repository = Substitute.For<IAccountRepository>();
        _publisher = Substitute.For<IEventPublisher>();
        _handler = new CreateContactHandler(_repository, _publisher);
    }

    private Account BuildAccount()
    {
        var normalizer = new NameNormalizer();
        return Account.Create(
            _tenantId, Guid.NewGuid(), AccountName.Create("Empresa Teste"), null, null, normalizer);
    }

    [Fact(DisplayName = "CreateContact: contato criado com sucesso e ContactLinked emitido com maskedDelta")]
    public async Task Handle_ValidCommand_CreatesContactAndPublishesEvent()
    {
        // Arrange
        var account = BuildAccount();
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        var command = new CreateContactCommand(
            AccountId: account.Id,
            Name: "João Silva",
            Email: "joao@empresa.com",
            Phone: "11999999999",
            Role: "Diretor");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        account.Contacts.Should().HaveCount(1);
        account.Contacts[0].Info.Name.Should().Be("João Silva");

        // ContactLinked deve ser emitido com maskedDelta (sem PII em claro — DD-003)
        await _publisher.Received(1).PublishAsync(
            Arg.Is<ContactLinked>(e =>
                e.AccountId == account.Id &&
                e.TenantId == _tenantId &&
                e.Action == "created" &&
                !e.MaskedDelta.Contains("João Silva") &&
                !e.MaskedDelta.Contains("joao@empresa.com")),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CreateContact: e-mail inválido → lança InvalidEmailException (ACC-ERR-004)")]
    public async Task Handle_InvalidEmail_ThrowsInvalidEmailException()
    {
        // Arrange — validator FluentValidation captura antes do handler,
        // mas o domínio também lança para segurança
        var account = BuildAccount();
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        var command = new CreateContactCommand(
            AccountId: account.Id,
            Name: "Maria Santos",
            Email: "email-invalido",
            Phone: null,
            Role: null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidEmailException>();
    }

    [Fact(DisplayName = "CreateContact: conta não encontrada → lança AccountNotFoundException (ACC-ERR-003)")]
    public async Task Handle_AccountNotFound_ThrowsAccountNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Account?)null);

        var command = new CreateContactCommand(
            AccountId: Guid.NewGuid(),
            Name: "Paulo Souza",
            Email: null,
            Phone: null,
            Role: null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AccountNotFoundException>();
    }
}
