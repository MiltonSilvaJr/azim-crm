using AccountManagement.Api.Tests.Helpers;
using AccountManagement.Application.Accounts.Commands.CreateAccount;
using AccountManagement.Application.Accounts.Commands.UpdateAccount;
using AccountManagement.Application.Accounts.Queries.GetAccountById;
using AccountManagement.Application.Accounts.Queries.SearchAccounts;
using AccountManagement.Application.Exceptions;
using AccountManagement.Contracts.Accounts;
using AccountManagement.Contracts.Common;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using FluentValidation;
using MediatR;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace AccountManagement.Api.Tests.Controllers;

/// <summary>
/// Testes de controller para <c>AccountsController</c>.
///
/// Verifica: códigos HTTP corretos, formato de erro padronizado, cabeçalho X-Correlation-Id,
/// isolamento de tenant, dedupe não-bloqueante, idempotência (ACC-ERR-009).
///
/// Mapeia: TASK-13, design §8, design §12, ACC-ERR-001/002/003/009.
/// </summary>
public sealed class AccountsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private static readonly NameNormalizer Normalizer = new();

    public AccountsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static Account BuildAccount(string name = "Azim Corp", Guid? tenantId = null) =>
        Account.Create(
            tenantId ?? TestWebApplicationFactory.DefaultTenantId,
            Guid.NewGuid(),
            AccountName.Create(name),
            website: null,
            notes: null,
            normalizer: Normalizer);

    private HttpClient CreateClient(string role = "Viewer") =>
        _factory.CreateClientWithRole(role);

    // =========================================================================
    // GET /api/v1/accounts — busca paginada
    // =========================================================================

    [Fact(DisplayName = "GET /api/v1/accounts retorna 200 com lista paginada do tenant")]
    public async Task GetAccounts_Returns200WithPagedList()
    {
        // Arrange
        var account = BuildAccount();
        _factory.Sender
            .Send(Arg.Any<SearchAccountsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Account> { account }.AsReadOnly() as IReadOnlyList<Account>);

        var client = CreateClient("Viewer");

        // Act
        var response = await client.GetAsync("/api/v1/accounts?search=Azim&page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("X-Correlation-Id");

        var body = await response.Content.ReadFromJsonAsync<AccountPageResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(1);
        body.Page.Should().Be(1);
        body.PageSize.Should().Be(10);
    }

    [Fact(DisplayName = "GET /api/v1/accounts com paginação inválida retorna 400 ACC-ERR-002")]
    public async Task GetAccounts_InvalidPagination_Returns400()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<SearchAccountsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FluentValidation.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("Page", "Parâmetros de busca inválidos.")
                    { ErrorCode = "ACC-ERR-002" },
            }));

        var client = CreateClient("Viewer");

        // Act
        var response = await client.GetAsync("/api/v1/accounts?page=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-002");
        error.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    // =========================================================================
    // POST /api/v1/accounts — criar conta
    // =========================================================================

    [Fact(DisplayName = "POST /api/v1/accounts com nome vazio retorna 400 ACC-ERR-001")]
    public async Task CreateAccount_EmptyName_Returns400AccErr001()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<CreateAccountCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FluentValidation.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("Name", "Nome da conta é obrigatório.")
                    { ErrorCode = "ACC-ERR-001" },
            }));

        var client = CreateClient("Vendedor");
        var request = new CreateAccountRequest(Name: "", Website: null, Notes: null);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/accounts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-001");
        error.Error.Should().NotBeNullOrWhiteSpace();
        error.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "POST /api/v1/accounts válido retorna 201 com Location")]
    public async Task CreateAccount_Valid_Returns201WithLocation()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        _factory.Sender
            .Send(Arg.Any<CreateAccountCommand>(), Arg.Any<CancellationToken>())
            .Returns(accountId);

        var client = CreateClient("Vendedor");
        var request = new CreateAccountRequest(Name: "Nova Empresa", Website: null, Notes: null);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/accounts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Should().ContainKey("X-Correlation-Id");
    }

    [Fact(DisplayName = "POST /api/v1/accounts com Idempotency-Key duplicada e payload divergente retorna 409 ACC-ERR-009")]
    public async Task CreateAccount_DuplicateIdempotencyKeyDivergentPayload_Returns409()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<CreateAccountCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Application.Exceptions.IdempotencyConflictException());

        var client = CreateClient("Vendedor");
        var request = new CreateAccountRequest(Name: "Empresa X", Website: null, Notes: null);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/accounts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-009");
    }

    // =========================================================================
    // GET /api/v1/accounts/{id} — detalhe
    // =========================================================================

    [Fact(DisplayName = "GET /api/v1/accounts/{id} retorna 200 para conta do tenant")]
    public async Task GetAccountById_OwnedByTenant_Returns200()
    {
        // Arrange
        var account = BuildAccount();
        _factory.Sender
            .Send(Arg.Any<GetAccountByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(account);

        var client = CreateClient("Viewer");

        // Act
        var response = await client.GetAsync($"/api/v1/accounts/{account.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AccountResponse>();
        body!.Id.Should().Be(account.Id);
        body.Name.Should().Be("Azim Corp");
    }

    [Fact(DisplayName = "GET /api/v1/accounts/{id} de outro tenant retorna 404 ACC-ERR-003")]
    public async Task GetAccountById_OtherTenant_Returns404AccErr003()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<GetAccountByIdQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AccountNotFoundException());

        var client = CreateClient("Viewer");

        // Act
        var response = await client.GetAsync($"/api/v1/accounts/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-003");
    }

    // =========================================================================
    // PATCH /api/v1/accounts/{id} — atualizar
    // =========================================================================

    [Fact(DisplayName = "PATCH /api/v1/accounts/{id} com nome vazio retorna 400 ACC-ERR-001")]
    public async Task UpdateAccount_EmptyName_Returns400AccErr001()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<UpdateAccountCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FluentValidation.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("Name", "Nome da conta é obrigatório.")
                    { ErrorCode = "ACC-ERR-001" },
            }));

        var client = CreateClient("Vendedor");
        var request = new UpdateAccountRequest(Name: "", Website: null, Notes: null);

        // Act
        var response = await client.PatchAsJsonAsync($"/api/v1/accounts/{Guid.NewGuid()}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-001");
    }

    [Fact(DisplayName = "PATCH /api/v1/accounts/{id} válido retorna 204")]
    public async Task UpdateAccount_Valid_Returns204()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<UpdateAccountCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Unit.Value));

        var client = CreateClient("Vendedor");
        var request = new UpdateAccountRequest(Name: "Nova Razão Social", Website: null, Notes: null);

        // Act
        var response = await client.PatchAsJsonAsync($"/api/v1/accounts/{Guid.NewGuid()}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // =========================================================================
    // Middleware — X-Correlation-Id e formato de erro
    // =========================================================================

    [Fact(DisplayName = "Toda resposta contém X-Correlation-Id")]
    public async Task AllResponses_ContainCorrelationIdHeader()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<GetAccountByIdQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AccountNotFoundException());

        var client = CreateClient("Viewer");

        // Act
        var response = await client.GetAsync($"/api/v1/accounts/{Guid.NewGuid()}");

        // Assert
        response.Headers.Should().ContainKey("X-Correlation-Id");
        var correlationId = response.Headers.GetValues("X-Correlation-Id").First();
        correlationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "Erros retornam formato { error, code, correlationId }")]
    public async Task Errors_ReturnStandardFormat()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<GetAccountByIdQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AccountNotFoundException());

        var client = CreateClient("Viewer");

        // Act
        var response = await client.GetAsync($"/api/v1/accounts/{Guid.NewGuid()}");

        // Assert
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().NotBeNullOrWhiteSpace();
        error.Code.Should().NotBeNullOrWhiteSpace();
        error.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }
}

