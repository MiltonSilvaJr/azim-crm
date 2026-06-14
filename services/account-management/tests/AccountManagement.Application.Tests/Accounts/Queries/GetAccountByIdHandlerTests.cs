using AccountManagement.Application.Accounts.Queries.GetAccountById;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace AccountManagement.Application.Tests.Accounts.Queries;

/// <summary>
/// Testes unitários para <see cref="GetAccountByIdHandler"/>.
///
/// Mapeia: TASK-04 ST-02, design §5.2, Req 3, ACC-ERR-003.
/// </summary>
public sealed class GetAccountByIdHandlerTests
{
    private readonly IAccountRepository _repository;
    private readonly GetAccountByIdHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public GetAccountByIdHandlerTests()
    {
        _repository = Substitute.For<IAccountRepository>();
        _handler = new GetAccountByIdHandler(_repository);
    }

    [Fact(DisplayName = "GetAccountById: conta existente é retornada")]
    public async Task Handle_ExistingAccount_ReturnsAccount()
    {
        // Arrange
        var normalizer = new NameNormalizer();
        var account = Account.Create(
            _tenantId, AccountName.Create("Empresa Teste"), null, null, normalizer);

        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        var query = new GetAccountByIdQuery(AccountId: account.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(account.Id);
        result.Name.Value.Should().Be("Empresa Teste");
    }

    [Fact(DisplayName = "GetAccountById: conta não encontrada ou fora do tenant lança AccountNotFoundException (ACC-ERR-003)")]
    public async Task Handle_AccountNotFoundOrOutOfTenant_ThrowsAccountNotFoundException()
    {
        // Arrange — repositório retorna null (não existe ou tenant diferente via filtro global)
        var unknownId = Guid.NewGuid();
        _repository.GetByIdAsync(unknownId, Arg.Any<CancellationToken>())
            .Returns((Account?)null);

        var query = new GetAccountByIdQuery(AccountId: unknownId);

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AccountNotFoundException>();
    }
}
