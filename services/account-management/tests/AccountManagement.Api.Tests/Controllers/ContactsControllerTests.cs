using AccountManagement.Api.Tests.Helpers;
using AccountManagement.Application.Contacts.Commands.CreateContact;
using FluentValidation;
using FsCheck.Fluent;
using AccountManagement.Application.Contacts.Commands.ForgetContact;
using AccountManagement.Application.Contacts.Commands.UpdateContact;
using AccountManagement.Application.Contacts.Queries.ListContacts;
using AccountManagement.Application.Exceptions;
using AccountManagement.Contracts.Common;
using AccountManagement.Contracts.Contacts;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Exceptions;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using MediatR;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace AccountManagement.Api.Tests.Controllers;

/// <summary>
/// Testes de controller para <c>ContactsController</c>.
///
/// Verifica: RBAC (Viewer/Vendedor/TenantAdmin), ForgetContact, anti-enumeração,
/// erros ACC-ERR-004..008, PBT-03 (nenhum endpoint retorna PII após anonimização).
///
/// Mapeia: TASK-14, design §8, design §12, ACC-ERR-004..008, PBT-03.
/// </summary>
public sealed class ContactsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private static readonly NameNormalizer Normalizer = new();
    private static readonly Guid AccountId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ContactId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public ContactsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static Contact BuildAnonymizedContact()
    {
        var account = Account.Create(
            TestWebApplicationFactory.DefaultTenantId,
            Guid.NewGuid(),
            AccountName.Create("Test Corp"),
            null, null, Normalizer);
        account.AddContact(
            ContactInfo.Create("João Silva", null, null),
            role: null);
        account.ForgetContact(
            account.Contacts[0].Id,
            TestWebApplicationFactory.DefaultUserId);
        return account.Contacts[0];
    }

    private static ContactResponse ToResponse(Contact c) =>
        new(c.Id, c.AccountId, c.Info.Name,
            c.Info.Email?.ToString(), c.Info.Phone?.ToString(),
            c.Role, c.PrivacyState.IsAnonymized,
            c.CreatedAt, c.UpdatedAt);

    private HttpClient CreateClient(string role) =>
        _factory.CreateClientWithRole(role);

    // =========================================================================
    // GET /api/v1/accounts/{id}/contacts — RBAC
    // =========================================================================

    [Fact(DisplayName = "GET contacts com Viewer retorna 403 ACC-ERR-008 sem revelar PII")]
    public async Task ListContacts_Viewer_Returns403()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<ListContactsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PiiAccessDeniedException());

        var client = CreateClient("Viewer");

        // Act
        var response = await client.GetAsync($"/api/v1/accounts/{AccountId}/contacts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-008");
        // Anti-enumeração: o corpo não deve revelar PII nem confirmar existência do contato
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("João Silva");
    }

    [Fact(DisplayName = "GET contacts com Vendedor retorna 200 com PII")]
    public async Task ListContacts_Vendedor_Returns200WithPii()
    {
        // Arrange
        var account = Account.Create(
            TestWebApplicationFactory.DefaultTenantId,
            Guid.NewGuid(),
            AccountName.Create("Corp"), null, null, Normalizer);
        account.AddContact(ContactInfo.Create("Maria", null, null), null);
        var contacts = account.Contacts;

        _factory.Sender
            .Send(Arg.Any<ListContactsQuery>(), Arg.Any<CancellationToken>())
            .Returns(contacts);

        var client = CreateClient("Vendedor");

        // Act
        var response = await client.GetAsync($"/api/v1/accounts/{AccountId}/contacts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<ContactResponse>>();
        items.Should().HaveCount(1);
        items![0].Name.Should().Be("Maria");
    }

    // =========================================================================
    // POST /api/v1/accounts/{id}/contacts — criar contato
    // =========================================================================

    [Fact(DisplayName = "POST contacts com e-mail inválido retorna 400 ACC-ERR-004")]
    public async Task CreateContact_InvalidEmail_Returns400AccErr004()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<CreateContactCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FluentValidation.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("Email", "E-mail do contato é inválido.")
                    { ErrorCode = "ACC-ERR-004" },
            }));

        var client = CreateClient("Vendedor");
        var request = new CreateContactRequest("João", "not-an-email", null, null);

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/accounts/{AccountId}/contacts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-004");
    }

    [Fact(DisplayName = "POST contacts com nome vazio retorna 400 ACC-ERR-005")]
    public async Task CreateContact_EmptyName_Returns400AccErr005()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<CreateContactCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FluentValidation.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("Name", "Nome do contato é obrigatório.")
                    { ErrorCode = "ACC-ERR-005" },
            }));

        var client = CreateClient("Vendedor");
        var request = new CreateContactRequest("", null, null, null);

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/accounts/{AccountId}/contacts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-005");
    }

    [Fact(DisplayName = "POST contacts válido retorna 201")]
    public async Task CreateContact_Valid_Returns201()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<CreateContactCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Unit.Value));

        var client = CreateClient("Vendedor");
        var request = new CreateContactRequest("Pedro Costa", "pedro@example.com", null, "Diretor");

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/accounts/{AccountId}/contacts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // =========================================================================
    // PATCH /api/v1/accounts/{accountId}/contacts/{id}
    // =========================================================================

    [Fact(DisplayName = "PATCH contact inexistente retorna 404 ACC-ERR-006")]
    public async Task UpdateContact_NotFound_Returns404AccErr006()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<UpdateContactCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ContactNotFoundException());

        var client = CreateClient("Vendedor");
        var request = new UpdateContactRequest("Novo Nome", null, null, null);

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/api/v1/accounts/{AccountId}/contacts/{ContactId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-006");
    }

    // =========================================================================
    // DELETE /api/v1/accounts/{accountId}/contacts/{id} — ForgetContact
    // =========================================================================

    [Fact(DisplayName = "DELETE contact com TenantAdmin retorna 204")]
    public async Task ForgetContact_TenantAdmin_Returns204()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<ForgetContactCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Unit.Value));

        var client = CreateClient("TenantAdmin");

        // Act
        var response = await client.DeleteAsync(
            $"/api/v1/accounts/{AccountId}/contacts/{ContactId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        // Nenhum PII no body
        var body = await response.Content.ReadAsStringAsync();
        body.Should().BeEmpty();
    }

    [Fact(DisplayName = "DELETE contact com Vendedor retorna 403 ACC-ERR-008")]
    public async Task ForgetContact_Vendedor_Returns403()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<ForgetContactCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PiiAccessDeniedException());

        var client = CreateClient("Vendedor");

        // Act
        var response = await client.DeleteAsync(
            $"/api/v1/accounts/{AccountId}/contacts/{ContactId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-008");
    }

    [Fact(DisplayName = "DELETE contact inexistente retorna 404 ACC-ERR-006")]
    public async Task ForgetContact_ContactNotFound_Returns404()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<ForgetContactCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ContactNotFoundException());

        var client = CreateClient("TenantAdmin");

        // Act
        var response = await client.DeleteAsync(
            $"/api/v1/accounts/{AccountId}/contacts/{ContactId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-006");
    }

    [Fact(DisplayName = "DELETE contact já anonimizado retorna 409 ACC-ERR-007")]
    public async Task ForgetContact_AlreadyAnonymized_Returns409()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<ForgetContactCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ContactAlreadyForgottenException());

        var client = CreateClient("TenantAdmin");

        // Act
        var response = await client.DeleteAsync(
            $"/api/v1/accounts/{AccountId}/contacts/{ContactId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-007");
    }

    // =========================================================================
    // PBT-03 — Após ForgetContact, nenhum endpoint retorna PII original
    // =========================================================================

    [Fact(DisplayName = "PBT-03: após ForgetContact, GET contacts retorna PII mascarada sem dados originais")]
    public async Task PBT03_AfterForget_GetContacts_ReturnsNoOriginalPii()
    {
        // Arrange — simula contato já anonimizado retornado pelo handler
        var anonymized = BuildAnonymizedContact();
        _factory.Sender
            .Send(Arg.Any<ListContactsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Contact> { anonymized }.AsReadOnly() as IReadOnlyList<Contact>);

        var client = CreateClient("Vendedor");

        // Act
        var response = await client.GetAsync($"/api/v1/accounts/{AccountId}/contacts");

        // Assert — PII original "João Silva" não deve aparecer
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("João Silva");
        body.Should().NotContain("joao.silva@example.com");

        // ContactId deve estar presente (integridade referencial — DD-001)
        body.Should().Contain(anonymized.Id.ToString());

        // Nome deve ser o marcador de anonimização
        var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<ContactResponse>>();
        items![0].Name.Should().Be(ContactInfo.AnonymizationMarker);
        items[0].IsAnonymized.Should().BeTrue();
    }

    [Property(DisplayName = "PBT-03: ContactResponse de contato anonimizado nunca contém PII original",
              Arbitrary = new[] { typeof(ContactPiiArbitrary) })]
    public void PBT03_AnonimizedContact_ResponseNeverContainsPii(string originalName)
    {
        // Arrange
        var account = Account.Create(
            TestWebApplicationFactory.DefaultTenantId,
            Guid.NewGuid(),
            AccountName.Create("Corp"), null, null, Normalizer);
        account.AddContact(ContactInfo.Create(originalName, null, null), null);
        var contact = account.Contacts[0];

        // Act — anonimizar
        account.ForgetContact(contact.Id, TestWebApplicationFactory.DefaultUserId);

        // Simular mapeamento para response (como o controller faria)
        var response = ToResponse(account.Contacts[0]);

        // Assert — PII original não está no response
        response.Name.Should().NotBe(originalName);
        response.Name.Should().Be(ContactInfo.AnonymizationMarker);
        response.Email.Should().BeNull();
        response.Phone.Should().BeNull();
        response.IsAnonymized.Should().BeTrue();
        // ContactId preservado (DD-001)
        response.Id.Should().Be(contact.Id);
    }
}

/// <summary>
/// Gerador FsCheck de nomes de contato com PII arbitrária para PBT-03.
/// Garante que nomes não sejam vazios (constraint do domínio).
/// </summary>
public static class ContactPiiArbitrary
{
    private static readonly string[] SampleNames =
    [
        "João Silva", "Maria Santos", "Pedro Oliveira",
        "Ana Costa", "Carlos Pereira", "Fernanda Lima",
    ];

    public static Arbitrary<string> Names()
    {
        // Usa ArbMap (API FsCheck 3.x compatível) para selecionar nome do pool
        var genInt = ArbMap.Default.ArbFor<int>().Generator;
        var gen = genInt.Select(i => SampleNames[Math.Abs(i % SampleNames.Length)]);
        return gen.ToArbitrary();
    }
}
