using AccountManagement.Application.Behaviors;
using AccountManagement.Application.Contacts.Commands.ForgetContact;
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
/// Testes unitários para <see cref="ForgetContactHandler"/>.
///
/// Mapeia: TASK-06 ST-02, design §5.1, Req 7, DD-001, ACC-ERR-006, ACC-ERR-007.
/// </summary>
public sealed class ForgetContactHandlerTests
{
    private readonly IAccountRepository _repository;
    private readonly IEventPublisher _publisher;
    private readonly UserContext _userContext;
    private readonly ForgetContactHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();

    public ForgetContactHandlerTests()
    {
        _repository = Substitute.For<IAccountRepository>();
        _publisher = Substitute.For<IEventPublisher>();
        _userContext = new UserContext();
        _userContext.SetUser(_adminId, "TenantAdmin");
        _handler = new ForgetContactHandler(_repository, _publisher, _userContext);
    }

    private Account BuildAccountWithContact()
    {
        var normalizer = new NameNormalizer();
        var account = Account.Create(
            _tenantId, Guid.NewGuid(), AccountName.Create("Empresa Teste"), null, null, normalizer);
        account.AddContact(
            ContactInfo.Create("Carlos Lima", Email.Create("carlos@empresa.com")),
            role: "Gerente");
        account.ClearDomainEvents(); // limpa eventos do AddContact
        return account;
    }

    [Fact(DisplayName = "ForgetContact: TenantAdmin anonimiza contato e emite ContactForgotten sem PII")]
    public async Task Handle_TenantAdmin_AnonymizesAndPublishesEvent()
    {
        // Arrange
        var account = BuildAccountWithContact();
        var contactId = account.Contacts[0].Id;

        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        var command = new ForgetContactCommand(
            AccountId: account.Id,
            ContactId: contactId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — PII substituída por marcador
        var contact = account.Contacts[0];
        contact.Info.Name.Should().Be(ContactInfo.AnonymizationMarker);
        contact.PrivacyState.IsAnonymized.Should().BeTrue();
        contact.Id.Should().Be(contactId); // contact_id preservado (DD-001)

        // ContactForgotten deve ser emitido sem PII
        await _publisher.Received(1).PublishAsync(
            Arg.Is<ContactForgotten>(e =>
                e.ContactId == contactId &&
                e.AccountId == account.Id &&
                e.TenantId == _tenantId &&
                e.RequestedBy == _adminId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ForgetContact: contato já anonimizado → lança ContactAlreadyForgottenException (ACC-ERR-007)")]
    public async Task Handle_AlreadyAnonymized_ThrowsContactAlreadyForgottenException()
    {
        // Arrange — anonimizar primeiro
        var account = BuildAccountWithContact();
        var contactId = account.Contacts[0].Id;
        account.ForgetContact(contactId, requestedBy: _adminId);
        account.ClearDomainEvents();

        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        var command = new ForgetContactCommand(
            AccountId: account.Id,
            ContactId: contactId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ContactAlreadyForgottenException>();
    }

    [Fact(DisplayName = "ForgetContact: contato não encontrado na conta → lança ContactNotFoundException (ACC-ERR-006)")]
    public async Task Handle_ContactNotFound_ThrowsContactNotFoundException()
    {
        // Arrange
        var account = BuildAccountWithContact();
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        var command = new ForgetContactCommand(
            AccountId: account.Id,
            ContactId: Guid.NewGuid()); // ID inexistente

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ContactNotFoundException>();
    }

    [Fact(DisplayName = "ForgetContact: ContactForgotten não carrega PII (RNF 1.2)")]
    public async Task Handle_ForgetContact_EventContainsNoPii()
    {
        // Arrange
        var account = BuildAccountWithContact();
        var contactId = account.Contacts[0].Id;

        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        ContactForgotten? capturedEvent = null;
        await _publisher.PublishAsync(
            Arg.Do<ContactForgotten>(e => capturedEvent = e),
            Arg.Any<CancellationToken>());

        var command = new ForgetContactCommand(AccountId: account.Id, ContactId: contactId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — evento sem PII em claro
        capturedEvent.Should().NotBeNull();
        capturedEvent!.ContactId.Should().Be(contactId);
        capturedEvent.RequestedBy.Should().Be(_adminId);
        // O evento não deve conter campos de PII (nome, e-mail, telefone)
        capturedEvent.Should().NotBeOfType<string>();
    }
}
