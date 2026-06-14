using AccountManagement.Application.Contacts.Queries.ListContacts;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace AccountManagement.Application.Tests.Contacts.Queries;

/// <summary>
/// Testes unitários para <see cref="ListContactsHandler"/>.
///
/// Mapeia: TASK-06 ST-03, design §5.2, Req 5, Req 9, RNF 6, ACC-ERR-003, ACC-ERR-008.
/// </summary>
public sealed class ListContactsHandlerTests
{
    private readonly IAccountRepository _repository;
    private readonly ListContactsHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public ListContactsHandlerTests()
    {
        _repository = Substitute.For<IAccountRepository>();
        _handler = new ListContactsHandler(_repository);
    }

    private Account BuildAccountWithContacts()
    {
        var normalizer = new NameNormalizer();
        var account = Account.Create(
            _tenantId, AccountName.Create("Empresa Teste"), null, null, normalizer);
        account.AddContact(ContactInfo.Create("Ana Costa"), role: "Diretora");
        account.AddContact(ContactInfo.Create("Rui Mendes"), role: "Gerente");
        account.ClearDomainEvents();
        return account;
    }

    [Fact(DisplayName = "ListContacts: retorna contatos da conta (PII disponível após PiiAccessBehavior)")]
    public async Task Handle_AuthorizedUser_ReturnsContacts()
    {
        // Arrange — PiiAccessBehavior já validou o papel antes de chegar aqui
        var account = BuildAccountWithContacts();
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        var query = new ListContactsQuery(AccountId: account.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(c => c.TenantId.Should().Be(_tenantId));
    }

    [Fact(DisplayName = "ListContacts: conta não encontrada → lança AccountNotFoundException (ACC-ERR-003)")]
    public async Task Handle_AccountNotFound_ThrowsAccountNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Account?)null);

        var query = new ListContactsQuery(AccountId: Guid.NewGuid());

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AccountNotFoundException>();
    }

    [Fact(DisplayName = "ListContacts: isolamento de tenant — repositório retorna somente contatos do tenant")]
    public async Task Handle_IsolatesContactsByTenant()
    {
        // Arrange — filtro global garante que GetByIdAsync só retorna contas do tenant correto
        var accountId = Guid.NewGuid();
        _repository.GetByIdAsync(accountId, Arg.Any<CancellationToken>())
            .Returns((Account?)null); // tenant diferente → null pelo filtro global

        var query = new ListContactsQuery(AccountId: accountId);

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AccountNotFoundException>();
    }
}
