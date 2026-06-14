using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
using MediatR;

namespace PartnerManagement.Api.Tests.Partners;

/// <summary>
/// Testes de integração para endpoints CRUD de parceiros.
/// TDD-first: ST-01 — Red escritos antes da implementação do controller.
/// Mapeia: TASK-23, Req 1, Req 2, Req 4, design §8.
/// </summary>
public sealed class PartnersCrudEndpointTests : IClassFixture<PartnerApiFactory>
{
    private readonly PartnerApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PartnersCrudEndpointTests(PartnerApiFactory factory)
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
    // POST /api/v1/partners — criar parceiro
    // =========================================================================

    [Fact]
    public async Task Post_WithValidRequest_Returns201WithLocation()
    {
        // Arrange
        var partnerId = Guid.NewGuid();
        _factory.Mediator
            .Send(Arg.Any<CreatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePartnerResult(partnerId, false));

        var client = CreateClientWithClaims(TestData.WriterClaims);
        var body = new CreatePartnerRequest
        {
            Name = "Acme Ltda",
            Role = "Indicador",
            PctSetup = 10m,
            PctRecorrente = 5m
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/partners", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain(partnerId.ToString());
    }

    [Fact]
    public async Task Post_WithEmptyName_Returns400WithPmErr001()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<CreatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Throws(new PartnerManagement.Domain.Partners.Exceptions.PartnerNameRequiredException());

        var client = CreateClientWithClaims(TestData.WriterClaims);
        var body = new CreatePartnerRequest { Name = "  ", Role = "Indicador" };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/partners", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("PM-ERR-001");
    }

    [Fact]
    public async Task Post_WithoutWritePermission_Returns403WithPmErr008()
    {
        // Arrange — Viewer não tem partners:write
        _factory.PermissionContext.HasPermission("partners:write").Returns(false);
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
    public async Task Post_WithoutToken_Returns401()
    {
        // Arrange — sem header de claims
        var client = _factory.CreateClient();
        var body = new CreatePartnerRequest { Name = "Acme", Role = "Indicador" };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/partners", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // GET /api/v1/partners — listar parceiros
    // =========================================================================

    [Fact]
    public async Task Get_List_DefaultReturnsOnlyActive()
    {
        // Arrange
        var partners = new ListPartnersResult(
            Partners:
            [
                new PartnerSummary(Guid.NewGuid(), "Acme", "Indicador", 10m, 5m, true, false)
            ],
            TotalCount: 1,
            Page: 1,
            PageSize: 20);

        _factory.Mediator
            .Send(Arg.Is<ListPartnersQuery>(q => q.Active == true), Arg.Any<CancellationToken>())
            .Returns(partners);

        var client = CreateClientWithClaims(TestData.ViewerClaims);

        // Act
        var response = await client.GetAsync("/api/v1/partners");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Acme");
    }

    [Fact]
    public async Task Get_List_WithInvalidPagination_Returns400WithPmErr009()
    {
        // Arrange — pageSize = 0 é inválido
        var client = CreateClientWithClaims(TestData.ViewerClaims);

        // Act
        var response = await client.GetAsync("/api/v1/partners?pageSize=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("PM-ERR-009");
    }

    // =========================================================================
    // GET /api/v1/partners/{id} — detalhe de parceiro
    // =========================================================================

    [Fact]
    public async Task GetById_WithValidId_Returns200()
    {
        // Arrange
        var partnerId = TestData.DefaultPartnerId;
        var detail = new PartnerDetail(
            partnerId, "Acme", "Indicador", 10m, 5m,
            null, null, null, true, false,
            DateTimeOffset.UtcNow, null);

        _factory.Mediator
            .Send(Arg.Is<GetPartnerByIdQuery>(q => q.PartnerId == partnerId), Arg.Any<CancellationToken>())
            .Returns(detail);

        var client = CreateClientWithClaims(TestData.ViewerClaims);

        // Act
        var response = await client.GetAsync($"/api/v1/partners/{partnerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(partnerId.ToString());
    }

    [Fact]
    public async Task GetById_WithUnknownId_Returns404WithPmErr007()
    {
        // Arrange
        var unknownId = Guid.NewGuid();
        _factory.Mediator
            .Send(Arg.Is<GetPartnerByIdQuery>(q => q.PartnerId == unknownId), Arg.Any<CancellationToken>())
            .Throws(new PartnerNotFoundException(unknownId));

        var client = CreateClientWithClaims(TestData.ViewerClaims);

        // Act
        var response = await client.GetAsync($"/api/v1/partners/{unknownId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("PM-ERR-007");
    }

    // =========================================================================
    // PATCH /api/v1/partners/{id} — atualizar parceiro
    // =========================================================================

    [Fact]
    public async Task Patch_WithValidRequest_Returns200()
    {
        // Arrange — PATCH busca estado atual internamente (GetPartnerByIdQuery) antes de atualizar
        var currentDetail = new PartnerDetail(
            TestData.DefaultPartnerId, "Acme", "Indicador", 10m, 5m,
            null, null, null, true, false, DateTimeOffset.UtcNow, null);

        _factory.Mediator
            .Send(Arg.Is<GetPartnerByIdQuery>(q => q.PartnerId == TestData.DefaultPartnerId), Arg.Any<CancellationToken>())
            .Returns(currentDetail);

        _factory.Mediator
            .Send(Arg.Any<UpdatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new UpdatePartnerResult());

        var client = CreateClientWithClaims(TestData.WriterClaims);
        var body = new UpdatePartnerRequest { Name = "Acme Editado" };
        var content = JsonContent.Create(body);

        // Act
        var response = await client.PatchAsync($"/api/v1/partners/{TestData.DefaultPartnerId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_WithUnknownId_Returns404WithPmErr007()
    {
        // Arrange — a primeira busca (GetPartnerById) já lança PartnerNotFoundException
        var unknownId = Guid.NewGuid();
        _factory.Mediator
            .Send(Arg.Is<GetPartnerByIdQuery>(q => q.PartnerId == unknownId), Arg.Any<CancellationToken>())
            .Throws(new PartnerNotFoundException(unknownId));

        var client = CreateClientWithClaims(TestData.WriterClaims);
        var body = new UpdatePartnerRequest { Name = "X" };
        var content = JsonContent.Create(body);

        // Act
        var response = await client.PatchAsync($"/api/v1/partners/{unknownId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        string responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("PM-ERR-007");
    }

    // =========================================================================
    // Formato de erro padrão
    // =========================================================================

    [Fact]
    public async Task AllErrors_ShouldContainCorrelationId()
    {
        // Arrange — forçar erro 404
        var unknownId = Guid.NewGuid();
        _factory.Mediator
            .Send(Arg.Is<GetPartnerByIdQuery>(q => q.PartnerId == unknownId), Arg.Any<CancellationToken>())
            .Throws(new PartnerNotFoundException(unknownId));

        var client = CreateClientWithClaims(TestData.ViewerClaims);
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "test-corr-001");

        // Act
        var response = await client.GetAsync($"/api/v1/partners/{unknownId}");

        // Assert
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("correlationId");
    }
}
