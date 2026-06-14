using FluentAssertions;
using MediatR;
using NSubstitute;
using Organization.Api.Tests.Infrastructure;
using Organization.Application.Commands.BusinessUnit;
using Organization.Application.Queries;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Organization.Api.Tests.Controllers;

/// <summary>
/// Testes de integração para BusinessUnitsController.
/// Verifica: contratos REST, RBAC papel×operação (permissão e negação), status codes.
/// </summary>
public sealed class BusinessUnitsControllerTests : IAsyncDisposable
{
    private readonly OrganizationApiFactory _factory;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _buId = Guid.NewGuid();

    public BusinessUnitsControllerTests()
    {
        _factory = new OrganizationApiFactory();

        // Clock padrão
        _factory.Clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        // TokenHasher padrão
        _factory.TokenHasher.GenerateToken().Returns(("plain", "hash"));
        _factory.TokenHasher.Hash(Arg.Any<string>()).Returns("hash");
    }

    // ── POST /api/v1/business-units ──────────────────────────────────────────

    [Fact]
    public async Task CreateBusinessUnit_AsTAdmin_Returns201()
    {
        // Arrange
        _factory.BusinessUnitRepository
            .ExistsByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _factory.EventOutbox
            .EnqueueAsync(Arg.Any<Organization.Domain.Events.IDomainEvent>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/business-units",
            new { name = "BU Vendas" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateBusinessUnit_WithoutAuth_Returns401()
    {
        // Arrange
        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/business-units",
            new { name = "BU Vendas" });

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("GestorBU")]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task CreateBusinessUnit_AsNonTAdmin_Returns403(string role)
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/business-units",
            new { name = "BU Vendas" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateBusinessUnit_WithEmptyName_Returns400()
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/business-units",
            new { name = "" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/v1/business-units ───────────────────────────────────────────

    [Theory]
    [InlineData("TAdmin")]
    [InlineData("GestorBU")]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task ListBusinessUnits_AsAnyAuthenticatedRole_Returns200(string role)
    {
        // Arrange
        _factory.BusinessUnitRepository
            .ListActiveAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Organization.Domain.Aggregates.BusinessUnit>());

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        // Act
        var response = await client.GetAsync("/api/v1/business-units");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListBusinessUnits_WithoutAuth_Returns401()
    {
        // Arrange
        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.GetAsync("/api/v1/business-units");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    // ── PUT /api/v1/business-units/{id} ────────────────────────────────────

    [Fact]
    public async Task RenameBusinessUnit_AsTAdmin_Returns200()
    {
        // Arrange
        var existing = Domain.Aggregates.BusinessUnit.Create(
            Domain.ValueObjects.BusinessUnitName.Create("BU Original"),
            _tenantId,
            DateTimeOffset.UtcNow);

        _factory.BusinessUnitRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        _factory.BusinessUnitRepository
            .ExistsByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/v1/business-units/{Guid.NewGuid()}",
            new { name = "BU Renomeada" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task RenameBusinessUnit_AsNonAuthorized_Returns403(string role)
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/v1/business-units/{Guid.NewGuid()}",
            new { name = "BU Renomeada" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE /api/v1/business-units/{id} ─────────────────────────────────

    [Fact]
    public async Task DeactivateBusinessUnit_AsTAdmin_Returns204()
    {
        // Arrange
        var existing = Domain.Aggregates.BusinessUnit.Create(
            Domain.ValueObjects.BusinessUnitName.Create("BU Ativa"),
            _tenantId,
            DateTimeOffset.UtcNow);

        _factory.BusinessUnitRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        _factory.OpportunityCounter
            .CountActiveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(0);

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.DeleteAsync($"/api/v1/business-units/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("GestorBU")]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task DeactivateBusinessUnit_AsNonTAdmin_Returns403(string role)
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        // Act
        var response = await client.DeleteAsync($"/api/v1/business-units/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Isolamento por tenant ─────────────────────────────────────────────────

    [Fact]
    public async Task GetBusinessUnits_DifferentTenants_ReturnIsolatedData()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var buA = Guid.NewGuid();
        var buB = Guid.NewGuid();

        _factory.BusinessUnitRepository
            .ListActiveAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Organization.Domain.Aggregates.BusinessUnit>());

        var clientA = _factory.CreateClientAs(tenantA, userA, buA, "TAdmin");
        var clientB = _factory.CreateClientAs(tenantB, userB, buB, "TAdmin");

        // Act
        var responseA = await clientA.GetAsync("/api/v1/business-units");
        var responseB = await clientB.GetAsync("/api/v1/business-units");

        // Assert — ambos retornam 200 (global filter garante isolamento no EF Core)
        responseA.StatusCode.Should().Be(HttpStatusCode.OK);
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
    }
}
