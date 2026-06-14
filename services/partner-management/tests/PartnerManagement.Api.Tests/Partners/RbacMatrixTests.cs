using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PartnerManagement.Application.Behaviors;
using PartnerManagement.Application.Partners;
using PartnerManagement.Application.Partners.Commands;
using PartnerManagement.Application.Partners.Queries;
using PartnerManagement.Api.Tests.Helpers;
using PartnerManagement.Contracts.Partners;
using Xunit;

namespace PartnerManagement.Api.Tests.Partners;

/// <summary>
/// Matriz RBAC — verifica que cada endpoint retorna 401/403 quando o papel é insuficiente
/// e 200/201 quando o papel é suficiente.
/// TDD-first: TASK-25 ST-01 Red.
/// Mapeia: TASK-25, Req 6, design §10, PartnerErrors catálogo.
/// </summary>
public sealed class RbacMatrixTests : IClassFixture<PartnerApiFactory>
{
    private readonly PartnerApiFactory _factory;

    public RbacMatrixTests(PartnerApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithClaims(string claims)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestClaimsHeader, claims);
        return client;
    }

    // =========================================================================
    // 401 — sem autenticação
    // =========================================================================

    [Theory]
    [InlineData("GET",  "/api/v1/partners")]
    [InlineData("POST", "/api/v1/partners")]
    [InlineData("GET",  "/api/v1/partners/22222222-2222-2222-2222-222222222222")]
    [InlineData("PATCH","/api/v1/partners/22222222-2222-2222-2222-222222222222")]
    [InlineData("POST", "/api/v1/partners/22222222-2222-2222-2222-222222222222/deactivate")]
    [InlineData("POST", "/api/v1/partners/22222222-2222-2222-2222-222222222222/reactivate")]
    [InlineData("GET",  "/api/v1/partners/22222222-2222-2222-2222-222222222222/eligibility")]
    [InlineData("GET",  "/api/v1/partners/22222222-2222-2222-2222-222222222222/commissions?from=2025-01-01&to=2025-01-31")]
    public async Task Unauthenticated_AllEndpoints_Return401(string method, string url)
    {
        // Arrange — sem header X-Test-Claims (não autenticado)
        var client = _factory.CreateClient();

        // Act
        var response = await SendAsync(client, method, url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: $"{method} {url} deve exigir autenticação");
    }

    // =========================================================================
    // Viewer (partners:read) — pode ler, não pode escrever ou ver comissões
    // =========================================================================

    [Fact]
    public async Task Viewer_ListPartners_Returns200()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<ListPartnersQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ListPartnersResult([], 0, 1, 20));

        var client = CreateClientWithClaims(TestData.ViewerClaims);

        // Act
        var response = await client.GetAsync("/api/v1/partners");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Viewer_GetById_Returns200()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<GetPartnerByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PartnerDetail(
                TestData.DefaultPartnerId, "Acme", "Indicador",
                10m, 5m, null, null, null, true, false,
                DateTimeOffset.UtcNow, null));

        var client = CreateClientWithClaims(TestData.ViewerClaims);

        // Act
        var response = await client.GetAsync($"/api/v1/partners/{TestData.DefaultPartnerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Viewer_CreatePartner_Returns403WhenBehaviorDenies()
    {
        // Arrange — AuthorizationBehavior mockado via AccessDeniedException
        _factory.Mediator
            .Send(Arg.Any<CreatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Throws(new AccessDeniedException("partners:write"));

        var client = CreateClientWithClaims(TestData.ViewerClaims);
        var body = new CreatePartnerRequest { Name = "Acme", Role = "Indicador" };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/partners", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("PM-ERR-008");
    }

    [Fact]
    public async Task Viewer_DeactivatePartner_Returns403WhenBehaviorDenies()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<DeactivatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Throws(new AccessDeniedException("partners:write"));

        var client = CreateClientWithClaims(TestData.ViewerClaims);

        // Act
        var response = await client.PostAsync(
            $"/api/v1/partners/{TestData.DefaultPartnerId}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("PM-ERR-008");
    }

    [Fact]
    public async Task Viewer_ReactivatePartner_Returns403WhenBehaviorDenies()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<ReactivatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Throws(new AccessDeniedException("partners:write"));

        var client = CreateClientWithClaims(TestData.ViewerClaims);

        // Act
        var response = await client.PostAsync(
            $"/api/v1/partners/{TestData.DefaultPartnerId}/reactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("PM-ERR-008");
    }

    [Fact]
    public async Task Viewer_GetCommissions_Returns403WhenBehaviorDenies()
    {
        // Arrange — Viewer não tem partners:commissions:read
        _factory.Mediator
            .Send(Arg.Any<GetPartnerCommissionViewQuery>(), Arg.Any<CancellationToken>())
            .Throws(new AccessDeniedException("partners:commissions:read"));

        var client = CreateClientWithClaims(TestData.ViewerClaims);
        string from = "2025-01-01";
        string to = "2025-01-31";

        // Act
        var response = await client.GetAsync(
            $"/api/v1/partners/{TestData.DefaultPartnerId}/commissions?from={from}&to={to}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("PM-ERR-008");
    }

    // =========================================================================
    // Writer (partners:read + partners:write) — pode escrever, não ver comissões
    // =========================================================================

    [Fact]
    public async Task Writer_CreatePartner_Returns201()
    {
        // Arrange
        var partnerId = Guid.NewGuid();
        _factory.Mediator
            .Send(Arg.Any<CreatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePartnerResult(partnerId, false));

        var client = CreateClientWithClaims(TestData.WriterClaims);
        var body = new CreatePartnerRequest { Name = "Acme", Role = "Indicador" };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/partners", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Writer_DeactivatePartner_Returns200()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<DeactivatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new DeactivatePartnerResult(TransitionEffective: true));

        var client = CreateClientWithClaims(TestData.WriterClaims);

        // Act
        var response = await client.PostAsync(
            $"/api/v1/partners/{TestData.DefaultPartnerId}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // Admin (todos os papéis) — acesso total
    // =========================================================================

    [Fact]
    public async Task Admin_GetCommissions_Returns200()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<GetPartnerCommissionViewQuery>(), Arg.Any<CancellationToken>())
            .Returns(new CommissionViewResult(
                TestData.DefaultPartnerId,
                DateTimeOffset.UtcNow.AddDays(-30),
                DateTimeOffset.UtcNow,
                ProjectedCommissionCents: 10000L,
                ConsolidatedCommissionCents: 20000L));

        var client = CreateClientWithClaims(TestData.AdminClaims);
        string from = "2025-01-01";
        string to = "2025-01-31";

        // Act
        var response = await client.GetAsync(
            $"/api/v1/partners/{TestData.DefaultPartnerId}/commissions?from={from}&to={to}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // Anti-enumeração: parceiro de outro tenant → 404 (não 403)
    // Impede que um ator autenticado descubra se um parceiro existe em outro tenant.
    // =========================================================================

    [Fact]
    public async Task GetById_PartnerFromOtherTenant_Returns404NotFoundInsteadOf403()
    {
        // Arrange — handler lança PartnerNotFoundException (não AccessDeniedException)
        // quando o parceiro não pertence ao tenant do contexto (Req 6.4, design §5.4)
        var otherTenantPartnerId = Guid.NewGuid();
        _factory.Mediator
            .Send(
                Arg.Is<GetPartnerByIdQuery>(q => q.PartnerId == otherTenantPartnerId),
                Arg.Any<CancellationToken>())
            .Throws(new PartnerNotFoundException(otherTenantPartnerId));

        var client = CreateClientWithClaims(TestData.ViewerClaims);

        // Act
        var response = await client.GetAsync($"/api/v1/partners/{otherTenantPartnerId}");

        // Assert — deve retornar 404, não 403 (anti-enumeração)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("PM-ERR-007");
        content.Should().NotContain("PM-ERR-008",
            because: "expor 403 revelaria que o recurso existe em outro tenant");
    }

    [Fact]
    public async Task Deactivate_PartnerFromOtherTenant_Returns404()
    {
        // Arrange
        var otherTenantPartnerId = Guid.NewGuid();
        _factory.Mediator
            .Send(
                Arg.Is<DeactivatePartnerCommand>(c => c.PartnerId == otherTenantPartnerId),
                Arg.Any<CancellationToken>())
            .Throws(new PartnerNotFoundException(otherTenantPartnerId));

        var client = CreateClientWithClaims(TestData.AdminClaims);

        // Act
        var response = await client.PostAsync(
            $"/api/v1/partners/{otherTenantPartnerId}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("PM-ERR-007");
    }

    // =========================================================================
    // Auxiliares
    // =========================================================================

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, string method, string url)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST" || method == "PATCH")
        {
            request.Content = JsonContent.Create(new { });
        }
        return await client.SendAsync(request);
    }
}
