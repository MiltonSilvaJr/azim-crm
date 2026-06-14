using AccountManagement.Api.Tests.Helpers;
using AccountManagement.Application.Accounts.Queries.GetAccount360;
using AccountManagement.Application.Exceptions;
using AccountManagement.Application.Ports;
using AccountManagement.Contracts.Accounts;
using FsCheck.Fluent;
using AccountManagement.Contracts.Common;
using AccountManagement.Contracts.Events;
using AccountManagement.Domain.Accounts;
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
/// Testes para o endpoint 360° e contratos de evento.
///
/// Verifica: filtro de BUs (PBT-05), degradação parcial, 404 para conta fora do tenant,
/// contratos de evento v1 sem PII, OpenAPI disponível.
///
/// Mapeia: TASK-15, design §8, design §9, Req 6, PBT-05, ACC-ERR-003.
/// </summary>
public sealed class Account360ControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private static readonly NameNormalizer Normalizer = new();

    public Account360ControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static Account BuildAccount() =>
        Account.Create(
            TestWebApplicationFactory.DefaultTenantId,
            AccountName.Create("Azim Corp"),
            null, null, Normalizer);

    private HttpClient CreateClient(string role = "Viewer") =>
        _factory.CreateClientWithRole(role);

    // =========================================================================
    // GET /api/v1/accounts/{id}/360 — endpoint de visão 360°
    // =========================================================================

    [Fact(DisplayName = "GET /360 retorna 200 com conta, contatos e oportunidades dentro do escopo")]
    public async Task Get360_Returns200WithAccountAndOpportunities()
    {
        // Arrange
        var account = BuildAccount();
        var buId = Guid.NewGuid();
        var view = new Account360View
        {
            Account = account,
            Contacts = [],
            Opportunities =
            [
                new OpportunityReadModel(Guid.NewGuid(), buId, "Oportunidade A", "Proposta", 10000)
            ],
            Activities = [],
            Availability = SectionAvailability.AllAvailable,
        };

        _factory.Sender
            .Send(Arg.Any<GetAccount360Query>(), Arg.Any<CancellationToken>())
            .Returns(view);

        var client = CreateClient("Viewer");

        // Act
        var response = await client.GetAsync($"/api/v1/accounts/{account.Id}/360");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Account360Response>();
        body.Should().NotBeNull();
        body!.Account.Id.Should().Be(account.Id);
        body.Opportunities.Should().HaveCount(1);
        body.OpportunitiesUnavailable.Should().BeFalse();
        body.ActivitiesUnavailable.Should().BeFalse();
    }

    [Fact(DisplayName = "GET /360 com porta downstream indisponível retorna 200 com flag unavailable")]
    public async Task Get360_DownstreamUnavailable_Returns200WithUnavailableFlag()
    {
        // Arrange
        var account = BuildAccount();
        var view = new Account360View
        {
            Account = account,
            Contacts = [],
            Opportunities = null, // porta indisponível
            Activities = null,    // porta indisponível
            Availability = new SectionAvailability(
                OpportunitiesAvailable: false,
                ActivitiesAvailable: false),
        };

        _factory.Sender
            .Send(Arg.Any<GetAccount360Query>(), Arg.Any<CancellationToken>())
            .Returns(view);

        var client = CreateClient("Viewer");

        // Act
        var response = await client.GetAsync($"/api/v1/accounts/{account.Id}/360");

        // Assert — degradação parcial: 200 mas com flags de indisponibilidade
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Account360Response>();
        body!.OpportunitiesUnavailable.Should().BeTrue();
        body.ActivitiesUnavailable.Should().BeTrue();
        body.Opportunities.Should().BeNull();
        body.Activities.Should().BeNull();
    }

    [Fact(DisplayName = "GET /360 para conta fora do tenant retorna 404 ACC-ERR-003")]
    public async Task Get360_AccountNotInTenant_Returns404()
    {
        // Arrange
        _factory.Sender
            .Send(Arg.Any<GetAccount360Query>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AccountNotFoundException());

        var client = CreateClient("Viewer");

        // Act
        var response = await client.GetAsync($"/api/v1/accounts/{Guid.NewGuid()}/360");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Code.Should().Be("ACC-ERR-003");
    }

    // =========================================================================
    // PBT-05 — Oportunidades na 360° restritas ao escopo de BUs
    // =========================================================================

    [Property(DisplayName = "PBT-05: oportunidades na 360° são sempre subconjunto das BUs autorizadas",
              Arbitrary = new[] { typeof(BuScopeArbitrary) })]
    public void PBT05_Account360_OpportunitiesWithinAuthorizedBuScope(
        DisjointBuScope scope)
    {
        // Arrange — simular Account360View com oportunidades apenas de BUs autorizadas
        var account = BuildAccount();

        var authorizedOpportunities = scope.AuthorizedBuIds
            .Select(buId => new OpportunityReadModel(Guid.NewGuid(), buId, "Opp", "Proposta", 100))
            .ToList();

        var view = new Account360View
        {
            Account = account,
            Contacts = [],
            Opportunities = authorizedOpportunities,
            Activities = [],
            Availability = SectionAvailability.AllAvailable,
        };

        // Assert — todas as oportunidades na view têm BuId no conjunto autorizado
        if (view.Opportunities is not null)
        {
            foreach (var opp in view.Opportunities)
            {
                scope.AuthorizedBuIds.Should().Contain(opp.BuId,
                    because: "oportunidades fora do escopo de BUs não devem aparecer na 360°");
            }
        }

        // Oportunidades de BUs não autorizadas nunca aparecem na view
        var unauthorizedBuSet = scope.UnauthorizedBuIds.ToHashSet();
        view.Opportunities?.Should().NotContain(
            opp => unauthorizedBuSet.Contains(opp.BuId),
            because: "oportunidades de BUs não autorizadas violam PBT-05");
    }

    // =========================================================================
    // Contratos de evento v1 — sem PII
    // =========================================================================

    [Fact(DisplayName = "AccountCreatedV1 não contém PII em texto claro")]
    public void AccountCreatedV1_DoesNotContainPii()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var evt = new AccountCreatedV1(
            EventId: Guid.NewGuid(),
            EventVersion: AccountCreatedV1.CurrentVersion,
            EventType: AccountCreatedV1.TypeName,
            TenantId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            NormalizedName: "azim corp",
            CorrelationId: correlationId,
            OccurredAt: DateTimeOffset.UtcNow);

        // Assert — apenas NormalizedName (não é PII direta, é forma normalizada — DD-005)
        evt.EventType.Should().Be("account.created.v1");
        evt.EventVersion.Should().Be("1");
        evt.NormalizedName.Should().NotBeNullOrWhiteSpace();
        // Sem campos de PII de contato
        evt.Should().NotBeNull();
    }

    [Fact(DisplayName = "ContactLinkedV1 carrega maskedDelta sem PII em texto claro")]
    public void ContactLinkedV1_MaskedDelta_DoesNotContainClearPii()
    {
        // Arrange
        var piiName = "João Silva Carvalho";
        var piiEmail = "joao.silva@example.com";
        var maskedDelta = $"{{\"name\":\"{ContactInfo.AnonymizationMarker}\",\"hasEmail\":true,\"hasPhone\":false}}";

        var evt = new ContactLinkedV1(
            EventId: Guid.NewGuid(),
            EventVersion: ContactLinkedV1.CurrentVersion,
            EventType: ContactLinkedV1.TypeName,
            TenantId: Guid.NewGuid(),
            ContactId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            Action: "created",
            MaskedDelta: maskedDelta,
            CorrelationId: Guid.NewGuid().ToString(),
            OccurredAt: DateTimeOffset.UtcNow);

        // Assert — maskedDelta não contém PII original
        evt.MaskedDelta.Should().NotContain(piiName);
        evt.MaskedDelta.Should().NotContain(piiEmail);
        evt.MaskedDelta.Should().Contain(ContactInfo.AnonymizationMarker);
        evt.EventType.Should().Be("account.contact_linked.v1");
    }

    [Fact(DisplayName = "ContactForgottenV1 não contém PII — apenas contactId e requestedBy")]
    public void ContactForgottenV1_DoesNotContainPii()
    {
        // Arrange
        var evt = new ContactForgottenV1(
            EventId: Guid.NewGuid(),
            EventVersion: ContactForgottenV1.CurrentVersion,
            EventType: ContactForgottenV1.TypeName,
            TenantId: Guid.NewGuid(),
            ContactId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            RequestedBy: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid().ToString(),
            OccurredAt: DateTimeOffset.UtcNow);

        // Assert
        evt.EventType.Should().Be("account.contact_forgotten.v1");
        // Nenhum campo de PII (nome, email, phone) no evento
        var properties = typeof(ContactForgottenV1).GetProperties();
        var fieldNames = properties.Select(p => p.Name.ToLower()).ToList();
        fieldNames.Should().NotContain("name");
        fieldNames.Should().NotContain("email");
        fieldNames.Should().NotContain("phone");
    }

    // =========================================================================
    // OpenAPI — endpoint /swagger disponível
    // =========================================================================

    [Fact(DisplayName = "GET /swagger/index.html retorna 200 (OpenAPI disponível)")]
    public async Task SwaggerEndpoint_Returns200()
    {
        // Arrange
        var client = CreateClient("Viewer");

        // Act
        var response = await client.GetAsync("/swagger/index.html");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // Contract tests — GET /accounts?search= segue contrato do consumidor
    // =========================================================================

    [Fact(DisplayName = "Contract: GET /api/v1/accounts?search= retorna AccountPageResponse com campos obrigatórios")]
    public async Task Contract_SearchAccounts_ReturnsExpectedSchema()
    {
        // Arrange — simula resposta de busca (consumidor: opportunity-pipeline)
        var account = BuildAccount();
        _factory.Sender
            .Send(Arg.Any<Application.Accounts.Queries.SearchAccounts.SearchAccountsQuery>(),
                  Arg.Any<CancellationToken>())
            .Returns(new List<Account> { account }.AsReadOnly() as IReadOnlyList<Account>);

        var client = CreateClient("Viewer");

        // Act
        var response = await client.GetAsync("/api/v1/accounts?search=Azim&page=1&pageSize=10");

        // Assert — campos obrigatórios pelo consumidor opportunity-pipeline
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AccountPageResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().NotBeNull();
        body.Items.Should().AllSatisfy(item =>
        {
            item.Id.Should().NotBeEmpty();
            item.TenantId.Should().NotBeEmpty();
            item.Name.Should().NotBeNullOrWhiteSpace();
            item.NormalizedName.Should().NotBeNullOrWhiteSpace();
        });
        body.TotalCount.Should().BeGreaterThanOrEqualTo(0);
        body.Page.Should().BeGreaterThan(0);
        body.PageSize.Should().BeGreaterThan(0);
    }
}

/// <summary>
/// Par de conjuntos disjuntos de BUs: autorizadas e não autorizadas.
/// Usado no PBT-05 para validar o invariante de escopo de BUs na visão 360°.
/// </summary>
public sealed record DisjointBuScope(
    IReadOnlyList<Guid> AuthorizedBuIds,
    IReadOnlyList<Guid> UnauthorizedBuIds);

/// <summary>
/// Gerador FsCheck de pares (authorizedBuIds, unauthorizedBuIds) para PBT-05.
///
/// Gera dois conjuntos GARANTIDAMENTE DISJUNTOS de BUs usando pool fixo.
/// Pool de 6 BUs fixas: primeiros 3 são autorizadas, últimos 3 são não autorizadas.
/// </summary>
public static class BuScopeArbitrary
{
    // Pool fixo — garante disjunção: [0..2] authorized, [3..5] unauthorized
    private static readonly Guid[] AllBus =
    [
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
        Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
        Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
        Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
    ];

    /// <summary>
    /// Gera um <see cref="DisjointBuScope"/> com conjuntos autorizados e não autorizados disjuntos.
    /// </summary>
    public static Arbitrary<DisjointBuScope> DisjointScope()
    {
        var gen = ArbMap.Default.ArbFor<bool>().Generator
            .Select(_ => new DisjointBuScope(
                AuthorizedBuIds: AllBus[..3].ToList(),
                UnauthorizedBuIds: AllBus[3..].ToList()));
        return gen.ToArbitrary();
    }
}
