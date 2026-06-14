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
using Xunit;

namespace PartnerManagement.Api.Tests.Partners;

/// <summary>
/// Testes de integração para endpoints de ação e consulta especializada:
/// deactivate, reactivate, eligibility, commissions.
/// TDD-first: ST-01 — Red escritos antes da implementação.
/// Mapeia: TASK-24, Req 3, Req 8, Req 9, Req 10, design §8.
/// </summary>
public sealed class PartnersActionEndpointTests : IClassFixture<PartnerApiFactory>
{
    private readonly PartnerApiFactory _factory;

    public PartnersActionEndpointTests(PartnerApiFactory factory)
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
    // POST /api/v1/partners/{id}/deactivate — inativar (idempotente)
    // =========================================================================

    [Fact]
    public async Task Deactivate_WithActivePartner_Returns200()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<DeactivatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new DeactivatePartnerResult(TransitionEffective: true));

        var client = CreateClientWithClaims(TestData.AdminClaims);

        // Act
        var response = await client.PostAsync($"/api/v1/partners/{TestData.DefaultPartnerId}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deactivate_WithAlreadyInactivePartner_Returns200Idempotent()
    {
        // Arrange — parceiro já inativo: idempotente, sem erro (DD-006, Req 3.5)
        _factory.Mediator
            .Send(Arg.Any<DeactivatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new DeactivatePartnerResult(TransitionEffective: false));

        var client = CreateClientWithClaims(TestData.AdminClaims);

        // Act
        var response = await client.PostAsync($"/api/v1/partners/{TestData.DefaultPartnerId}/deactivate", null);

        // Assert — 200 mesmo para operação idempotente (não 409)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deactivate_WithUnknownId_Returns404WithPmErr007()
    {
        // Arrange
        var unknownId = Guid.NewGuid();
        _factory.Mediator
            .Send(Arg.Any<DeactivatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Throws(new PartnerNotFoundException(unknownId));

        var client = CreateClientWithClaims(TestData.AdminClaims);

        // Act
        var response = await client.PostAsync($"/api/v1/partners/{unknownId}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("PM-ERR-007");
    }

    // =========================================================================
    // POST /api/v1/partners/{id}/reactivate — reativar (idempotente)
    // =========================================================================

    [Fact]
    public async Task Reactivate_WithInactivePartner_Returns200()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<ReactivatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ReactivatePartnerResult(TransitionEffective: true));

        var client = CreateClientWithClaims(TestData.AdminClaims);

        // Act
        var response = await client.PostAsync($"/api/v1/partners/{TestData.DefaultPartnerId}/reactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reactivate_WithAlreadyActivePartner_Returns200Idempotent()
    {
        // Arrange — idempotente (DD-006)
        _factory.Mediator
            .Send(Arg.Any<ReactivatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ReactivatePartnerResult(TransitionEffective: false));

        var client = CreateClientWithClaims(TestData.AdminClaims);

        // Act
        var response = await client.PostAsync($"/api/v1/partners/{TestData.DefaultPartnerId}/reactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // GET /api/v1/partners/{id}/eligibility — elegibilidade (mTLS interno)
    // =========================================================================

    [Fact]
    public async Task Eligibility_WithActivePartner_ReturnsActiveTrue()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Is<GetPartnerEligibilityQuery>(q => q.PartnerId == TestData.DefaultPartnerId), Arg.Any<CancellationToken>())
            .Returns(new PartnerEligibilityResult(TestData.DefaultPartnerId, Active: true));

        var client = CreateClientWithClaims(TestData.ViewerClaims);

        // Act
        var response = await client.GetAsync($"/api/v1/partners/{TestData.DefaultPartnerId}/eligibility");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("true");
    }

    [Fact]
    public async Task Eligibility_WithInactivePartner_ReturnsActiveFalse()
    {
        // Arrange — parceiro inativo: pipeline deve bloquear vínculo (DD-007)
        var inactiveId = Guid.NewGuid();
        _factory.Mediator
            .Send(Arg.Is<GetPartnerEligibilityQuery>(q => q.PartnerId == inactiveId), Arg.Any<CancellationToken>())
            .Returns(new PartnerEligibilityResult(inactiveId, Active: false));

        var client = CreateClientWithClaims(TestData.ViewerClaims);

        // Act
        var response = await client.GetAsync($"/api/v1/partners/{inactiveId}/eligibility");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("false");
    }

    [Fact]
    public async Task Eligibility_WithUnknownId_Returns404WithPmErr007()
    {
        // Arrange
        var unknownId = Guid.NewGuid();
        _factory.Mediator
            .Send(Arg.Is<GetPartnerEligibilityQuery>(q => q.PartnerId == unknownId), Arg.Any<CancellationToken>())
            .Throws(new PartnerNotFoundException(unknownId));

        var client = CreateClientWithClaims(TestData.ViewerClaims);

        // Act
        var response = await client.GetAsync($"/api/v1/partners/{unknownId}/eligibility");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("PM-ERR-007");
    }

    // =========================================================================
    // GET /api/v1/partners/{id}/commissions — visão de comissão
    // =========================================================================

    [Fact]
    public async Task Commissions_WithValidPeriod_Returns200WithCents()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<GetPartnerCommissionViewQuery>(), Arg.Any<CancellationToken>())
            .Returns(new CommissionViewResult(
                TestData.DefaultPartnerId,
                DateTimeOffset.UtcNow.AddDays(-30),
                DateTimeOffset.UtcNow,
                ProjectedCommissionCents: 150000L,
                ConsolidatedCommissionCents: 300000L));

        var client = CreateClientWithClaims(TestData.AdminClaims);
        string from = DateTimeOffset.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
        string to = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");

        // Act
        var response = await client.GetAsync(
            $"/api/v1/partners/{TestData.DefaultPartnerId}/commissions?from={from}&to={to}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("150000");
    }

    [Fact]
    public async Task Commissions_WithoutPeriod_Returns400WithPmErr011()
    {
        // Arrange — período ausente deve retornar PM-ERR-011
        var client = CreateClientWithClaims(TestData.AdminClaims);

        // Act — sem from/to
        var response = await client.GetAsync(
            $"/api/v1/partners/{TestData.DefaultPartnerId}/commissions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("PM-ERR-011");
    }

    [Fact]
    public async Task Commissions_WithViewerRole_Returns403WithPmErr008()
    {
        // Arrange — Viewer não tem partners:commissions:read
        _factory.Mediator
            .Send(Arg.Any<GetPartnerCommissionViewQuery>(), Arg.Any<CancellationToken>())
            .Throws(new AccessDeniedException("partners:commissions:read"));

        var client = CreateClientWithClaims(TestData.ViewerClaims);
        string from = DateTimeOffset.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
        string to = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");

        // Act
        var response = await client.GetAsync(
            $"/api/v1/partners/{TestData.DefaultPartnerId}/commissions?from={from}&to={to}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("PM-ERR-008");
    }

    [Fact]
    public async Task Commissions_WhenReadPortUnavailable_Returns200WithUnavailableFlag()
    {
        // Arrange — degradação parcial: read port indisponível mas cadastro disponível (design §5.3)
        _factory.Mediator
            .Send(Arg.Any<GetPartnerCommissionViewQuery>(), Arg.Any<CancellationToken>())
            .Returns(new CommissionViewResult(
                TestData.DefaultPartnerId,
                DateTimeOffset.UtcNow.AddDays(-30),
                DateTimeOffset.UtcNow,
                ProjectedCommissionCents: 0L,
                ConsolidatedCommissionCents: 0L,
                CommissionUnavailable: true));

        var client = CreateClientWithClaims(TestData.AdminClaims);
        string from = DateTimeOffset.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
        string to = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");

        // Act
        var response = await client.GetAsync(
            $"/api/v1/partners/{TestData.DefaultPartnerId}/commissions?from={from}&to={to}");

        // Assert — 200 (não 5xx) com flag de indisponibilidade
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("commissionUnavailable");
    }
}
