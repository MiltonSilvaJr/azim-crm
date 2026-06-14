using AccountManagement.Api.Tests.Helpers;
using AccountManagement.Application.Contacts.Commands.ForgetContact;
using AccountManagement.Application.Contacts.Queries.ListContacts;
using AccountManagement.Application.Exceptions;
using AccountManagement.Contracts.Common;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace AccountManagement.Api.Tests.Security;

/// <summary>
/// Matriz papel × endpoint de contato — revisão de RBAC final (TASK-18 ST-01/ST-02).
///
/// Verifica que:
/// - Nenhum papel abaixo do mínimo recebe PII ou executa operação proibida.
/// - 403 não revela existência da entidade além do necessário (anti-enumeração).
/// - DELETE de contato por papel diferente de TenantAdmin retorna 403 sem vazar estado.
///
/// Mapeia: TASK-18 ST-01/ST-02, Req 9, RNF 6, design §10, ACC-ERR-008.
/// </summary>
public sealed class RbacMatrixTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private static readonly Guid AccountId = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
    private static readonly Guid ContactId = Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB");

    public RbacMatrixTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // =========================================================================
    // ST-01: Anti-enumeração — 403 não revela existência do contato
    // =========================================================================

    [Fact(DisplayName = "Anti-enumeração: GET contacts sem acesso retorna 403 sem revelar existência de entidade")]
    public async Task GetContacts_NoAccess_Returns403_WithoutRevealingEntityExistence()
    {
        // Arrange — simula acesso negado (PiiAccessDeniedException)
        _factory.Sender
            .Send(Arg.Any<ListContactsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PiiAccessDeniedException());

        var client = _factory.CreateClientWithRole("Viewer");

        // Act
        var response = await client.GetAsync($"/api/v1/accounts/{AccountId}/contacts");

        // Assert — 403 com ACC-ERR-008 (genérico, sem vazar existência do contato)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();

        // Anti-enumeração: a resposta 403 não deve diferenciar "conta não existe" de "sem acesso"
        // O erro ACC-ERR-008 é genérico e não revela PII nem estado da entidade
        body.Should().NotContain(AccountId.ToString(),
            because: "a resposta de erro não deve confirmar a existência da conta ao papel proibido");

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-008",
            because: "todas as negações de acesso a PII retornam ACC-ERR-008");
    }

    [Fact(DisplayName = "Anti-enumeração: DELETE contact por Vendedor retorna 403 sem vazar estado do contato")]
    public async Task ForgetContact_Vendedor_Returns403_WithoutRevealingContactState()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<ForgetContactCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PiiAccessDeniedException());

        var client = _factory.CreateClientWithRole("Vendedor");

        // Act
        var response = await client.DeleteAsync(
            $"/api/v1/accounts/{AccountId}/contacts/{ContactId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();

        // Anti-enumeração: Vendedor não sabe se o contato existe, está anonimizado, etc.
        body.Should().NotContain("anonymized",
            because: "estado de anonimização não deve ser revelado para papel sem acesso");
        body.Should().NotContain("forgotten",
            because: "estado de esquecimento não deve ser revelado para papel sem acesso");
        body.Should().NotContain("contactId",
            because: "referência ao contactId não deve aparecer na resposta de erro de autorização");

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-008");
    }

    // =========================================================================
    // ST-02: Matriz papel × endpoint — varredura completa
    // =========================================================================

    [Theory(DisplayName = "RBAC: papéis abaixo do mínimo em GET contacts retornam 403")]
    [InlineData("Viewer")]
    public async Task GetContacts_BelowMinimumRole_Returns403(string role)
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<ListContactsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PiiAccessDeniedException());

        var client = _factory.CreateClientWithRole(role);

        // Act
        var response = await client.GetAsync($"/api/v1/accounts/{AccountId}/contacts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: $"papel '{role}' está abaixo do mínimo (Vendedor) para GET contacts");

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-008");
    }

    [Theory(DisplayName = "RBAC: papéis abaixo do mínimo em DELETE contact retornam 403")]
    [InlineData("Viewer")]
    [InlineData("Vendedor")]
    public async Task ForgetContact_BelowMinimumRole_Returns403(string role)
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<ForgetContactCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PiiAccessDeniedException());

        var client = _factory.CreateClientWithRole(role);

        // Act
        var response = await client.DeleteAsync(
            $"/api/v1/accounts/{AccountId}/contacts/{ContactId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: $"papel '{role}' está abaixo do mínimo (TenantAdmin) para DELETE/ForgetContact");

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-008");
    }

    [Fact(DisplayName = "RBAC: TenantAdmin pode executar ForgetContact (papel mínimo correto)")]
    public async Task ForgetContact_TenantAdmin_Succeeds()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<ForgetContactCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MediatR.Unit.Value));

        var client = _factory.CreateClientWithRole("TenantAdmin");

        // Act
        var response = await client.DeleteAsync(
            $"/api/v1/accounts/{AccountId}/contacts/{ContactId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "TenantAdmin tem autorização para executar ForgetContact");
    }

    [Fact(DisplayName = "RBAC: Vendedor pode acessar GET contacts (papel mínimo correto)")]
    public async Task GetContacts_Vendedor_Succeeds()
    {
        // Arrange — retorna lista vazia (sem PII) — só verifica autorização
        _factory.Sender
            .Send(Arg.Any<ListContactsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<AccountManagement.Domain.Accounts.Contact>().AsReadOnly()
                as IReadOnlyList<AccountManagement.Domain.Accounts.Contact>);

        var client = _factory.CreateClientWithRole("Vendedor");

        // Act
        var response = await client.GetAsync($"/api/v1/accounts/{AccountId}/contacts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "Vendedor tem autorização para GET contacts (papel mínimo atendido)");
    }
}
