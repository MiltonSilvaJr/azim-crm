using AccountManagement.Application.Accounts.Queries.SearchAccounts;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace AccountManagement.Application.Tests.Accounts.Queries;

/// <summary>
/// Testes unitários para <see cref="SearchAccountsHandler"/> e <see cref="SearchSimilarAccountsHandler"/>.
///
/// Mapeia: TASK-04 ST-02, design §5.2, Req 3, DD-006, ACC-ERR-002.
/// </summary>
public sealed class SearchAccountsHandlerTests
{
    private readonly IAccountRepository _repository;
    private readonly SearchAccountsHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public SearchAccountsHandlerTests()
    {
        _repository = Substitute.For<IAccountRepository>();
        _handler = new SearchAccountsHandler(_repository);
    }

    private Account BuildAccount(string name)
    {
        var normalizer = new NameNormalizer();
        return Account.Create(_tenantId, AccountName.Create(name), null, null, normalizer);
    }

    [Fact(DisplayName = "SearchAccounts: retorna página paginada restrita ao tenant")]
    public async Task Handle_ValidQuery_ReturnsPaginatedResults()
    {
        // Arrange
        var accounts = new List<Account>
        {
            BuildAccount("Conta Alpha"),
            BuildAccount("Conta Beta")
        };
        _repository.SearchAsync("conta", 1, 10, Arg.Any<CancellationToken>())
            .Returns(accounts.AsReadOnly());

        var query = new SearchAccountsQuery(Search: "conta", Page: 1, PageSize: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(a => a.TenantId.Should().Be(_tenantId));
    }

    [Fact(DisplayName = "SearchAccounts: busca vazia retorna todas as contas do tenant")]
    public async Task Handle_NullSearch_ReturnsAllTenantAccounts()
    {
        // Arrange
        _repository.SearchAsync(null, 1, 20, Arg.Any<CancellationToken>())
            .Returns(new List<Account> { BuildAccount("Empresa X") }.AsReadOnly());

        var query = new SearchAccountsQuery(Search: null, Page: 1, PageSize: 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        await _repository.Received(1).SearchAsync(null, 1, 20, Arg.Any<CancellationToken>());
    }
}

/// <summary>
/// Testes para <see cref="SearchSimilarAccountsHandler"/>.
///
/// Mapeia: TASK-04 ST-02, design §5.2, Req 1.2, DD-006.
/// </summary>
public sealed class SearchSimilarAccountsHandlerTests
{
    private readonly IAccountRepository _repository;
    private readonly SearchSimilarAccountsHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public SearchSimilarAccountsHandlerTests()
    {
        _repository = Substitute.For<IAccountRepository>();
        _handler = new SearchSimilarAccountsHandler(_repository);
    }

    [Fact(DisplayName = "SearchSimilarAccounts: retorna candidatos do mesmo tenant com mesmo normalized_name")]
    public async Task Handle_ValidQuery_ReturnsSimilarAccountsInTenant()
    {
        // Arrange
        var normalizer = new NameNormalizer();
        var existingAccount = Account.Create(
            _tenantId, AccountName.Create("Pag AI"), null, null, normalizer);

        _repository.SearchSimilarAsync(
                Arg.Is<NormalizedName>(n => n.Value == "pag ai"),
                Arg.Any<CancellationToken>())
            .Returns(new List<Account> { existingAccount }.AsReadOnly());

        var query = new SearchSimilarAccountsQuery(Name: "Pag AI");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().NormalizedName.Value.Should().Be("pag ai");
    }

    [Fact(DisplayName = "SearchSimilarAccounts: nome com acento/caixa normalizado antes da busca")]
    public async Task Handle_NameWithAccents_NormalizesBeforeSearch()
    {
        // Arrange
        // "Pág.AI Tecnologia" → NFD sem acento → minúsculas → remove . → "pagai tecnologia"
        _repository.SearchSimilarAsync(
                Arg.Is<NormalizedName>(n => n.Value == "pagai tecnologia"),
                Arg.Any<CancellationToken>())
            .Returns(new List<Account>().AsReadOnly());

        var query = new SearchSimilarAccountsQuery(Name: "Pág.AI Tecnologia");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        await _repository.Received(1).SearchSimilarAsync(
            Arg.Is<NormalizedName>(n => n.Value == "pagai tecnologia"),
            Arg.Any<CancellationToken>());
    }
}
