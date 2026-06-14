using FluentAssertions;
using NSubstitute;
using Organization.Api.Tests.Infrastructure;
using Organization.Domain.Aggregates;
using Organization.Domain.Policies;
using Organization.Domain.ValueObjects;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Organization.Api.Tests.Controllers;

/// <summary>
/// Testes de integração para StagesController, OriginChannelsController, LossReasonsController.
/// Verifica: RBAC TAdmin/GestorBU, contratos REST, erros ORG-ERR-013..017.
/// </summary>
public sealed class PipelineConfigControllerTests : IAsyncDisposable
{
    private readonly OrganizationApiFactory _factory;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _buId = Guid.NewGuid();

    public PipelineConfigControllerTests()
    {
        _factory = new OrganizationApiFactory();
        _factory.Clock.UtcNow.Returns(DateTimeOffset.UtcNow);
    }

    private BusinessUnit CreateTestBusinessUnit()
    {
        var bu = BusinessUnit.Create(
            BusinessUnitName.Create("BU Teste"),
            _tenantId,
            DateTimeOffset.UtcNow);

        // Seed mínimo para que TerminalStagesPolicy seja satisfeita
        foreach (var seed in StageSeedFactory.CreateDefaultStages())
            bu.AddStage(seed.Name, seed.Probability, seed.Category, seed.Position, Guid.NewGuid());

        bu.AddOriginChannel("Site", Guid.NewGuid());
        bu.AddLossReason("Preço", Guid.NewGuid());

        return bu;
    }

    // ── GET /api/v1/business-units/{buId}/stages ─────────────────────────────

    [Fact]
    public async Task ListStages_AnonymousAccess_Returns200()
    {
        // Arrange — leitura pública (sem JWT)
        var bu = CreateTestBusinessUnit();
        _factory.BusinessUnitRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(bu);

        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.GetAsync($"/api/v1/business-units/{_buId}/stages");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListStages_OrderedByPosition_Returns200()
    {
        // Arrange
        var bu = CreateTestBusinessUnit();
        _factory.BusinessUnitRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(bu);

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.GetAsync($"/api/v1/business-units/{_buId}/stages");
        var stages = await response.Content.ReadFromJsonAsync<List<StageResultDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stages.Should().NotBeNull();
        stages!.Should().BeInAscendingOrder(s => s.Position);
    }

    // ── POST /api/v1/business-units/{buId}/stages ────────────────────────────

    [Fact]
    public async Task AddStage_AsTAdmin_Returns201()
    {
        // Arrange
        var bu = CreateTestBusinessUnit();
        _factory.BusinessUnitRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(bu);

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/business-units/{_buId}/stages",
            new { name = "Qualificação", probabilityPercent = 40, category = "open", position = 9 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddStage_AsGestorBU_Returns201()
    {
        // Arrange
        var bu = CreateTestBusinessUnit();
        _factory.BusinessUnitRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(bu);

        // GestorBU da mesma BU
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "GestorBU");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/business-units/{_buId}/stages",
            new { name = "Qualificação", probabilityPercent = 40, category = "open", position = 9 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task AddStage_AsNonAuthorized_Returns403(string role)
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/business-units/{_buId}/stages",
            new { name = "Estágio", probabilityPercent = 50, category = "open", position = 9 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddStage_WithInvalidCategory_Returns400()
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/business-units/{_buId}/stages",
            new { name = "Estágio", probabilityPercent = 50, category = "invalid", position = 9 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── DELETE /api/v1/business-units/{buId}/stages/{id} ────────────────────

    [Fact]
    public async Task RemoveStage_AsTAdmin_Returns204()
    {
        // Arrange
        var bu = CreateTestBusinessUnit();
        var stageId = bu.Stages.First(s => s.Category.Value == "open").Id;

        _factory.BusinessUnitRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(bu);

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.DeleteAsync(
            $"/api/v1/business-units/{_buId}/stages/{stageId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task RemoveStage_AsNonAuthorized_Returns403(string role)
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        // Act
        var response = await client.DeleteAsync(
            $"/api/v1/business-units/{_buId}/stages/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/v1/business-units/{buId}/origin-channels ───────────────────

    [Fact]
    public async Task ListOriginChannels_AnonymousAccess_Returns200()
    {
        // Arrange
        var bu = CreateTestBusinessUnit();
        _factory.BusinessUnitRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(bu);

        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.GetAsync($"/api/v1/business-units/{_buId}/origin-channels");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AddOriginChannel_AsTAdmin_Returns201()
    {
        // Arrange
        var bu = CreateTestBusinessUnit();
        _factory.BusinessUnitRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(bu);

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/business-units/{_buId}/origin-channels",
            new { name = "LinkedIn" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("GestorBU")]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task AddOriginChannel_AsNonTAdmin_Returns403(string role)
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/business-units/{_buId}/origin-channels",
            new { name = "LinkedIn" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/v1/business-units/{buId}/loss-reasons ──────────────────────

    [Fact]
    public async Task ListLossReasons_AnonymousAccess_Returns200()
    {
        // Arrange
        var bu = CreateTestBusinessUnit();
        _factory.BusinessUnitRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(bu);

        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.GetAsync($"/api/v1/business-units/{_buId}/loss-reasons");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AddLossReason_AsTAdmin_Returns201()
    {
        // Arrange
        var bu = CreateTestBusinessUnit();
        _factory.BusinessUnitRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(bu);

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/business-units/{_buId}/loss-reasons",
            new { name = "Concorrência" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("GestorBU")]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task AddLossReason_AsNonTAdmin_Returns403(string role)
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/business-units/{_buId}/loss-reasons",
            new { name = "Concorrência" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Contrato REST — Problem Details ──────────────────────────────────────

    [Fact]
    public async Task AllErrors_ReturnProblemDetails_WithCodeField()
    {
        // Arrange — RBAC negado deve retornar Problem Details com campo "code"
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "Viewer");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/business-units/{_buId}/stages",
            new { name = "Estágio", probabilityPercent = 50, category = "open", position = 9 });

        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should().Contain("code");
        body.Should().Contain("ORG-ERR");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    // DTO local para deserialização da resposta de stages
    private sealed record StageResultDto(Guid Id, string Name, int ProbabilityPercent, string Category, int Position);
}
