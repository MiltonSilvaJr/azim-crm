using FluentAssertions;
using NSubstitute;
using Organization.Api.Tests.Infrastructure;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Organization.Api.Tests.Controllers;

/// <summary>
/// Testes de integração para MembershipsController.
/// Verifica: RBAC TAdmin obrigatório, contratos REST, ORG-ERR-009/012.
/// </summary>
public sealed class MembershipsControllerTests : IAsyncDisposable
{
    private readonly OrganizationApiFactory _factory;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _buId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();

    public MembershipsControllerTests()
    {
        _factory = new OrganizationApiFactory();
        _factory.Clock.UtcNow.Returns(DateTimeOffset.UtcNow);
    }

    // ── GET /api/v1/users/{id}/memberships ──────────────────────────────────

    [Fact]
    public async Task ListMemberships_AsTAdmin_Returns200()
    {
        // Arrange
        var user = User.Activate("user@example.com", "User Test", "uid123", _tenantId, DateTimeOffset.UtcNow);
        _factory.UserRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(user);

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.GetAsync($"/api/v1/users/{_targetUserId}/memberships");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("GestorBU")]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task ListMemberships_AsNonTAdmin_Returns403(string role)
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        // Act
        var response = await client.GetAsync($"/api/v1/users/{_targetUserId}/memberships");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── POST /api/v1/users/{id}/memberships ─────────────────────────────────

    [Fact]
    public async Task AssignMembership_AsTAdmin_Returns201()
    {
        // Arrange
        var user = User.Activate("user@example.com", "User Test", "uid123", _tenantId, DateTimeOffset.UtcNow);
        _factory.UserRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(user);

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/users/{_targetUserId}/memberships",
            new { buId = _buId, role = "Vendedor" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AssignMembership_WithInvalidRole_Returns400()
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/users/{_targetUserId}/memberships",
            new { buId = _buId, role = "PlatOp" }); // Papel inválido

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("GestorBU")]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task AssignMembership_AsNonTAdmin_Returns403(string role)
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/users/{_targetUserId}/memberships",
            new { buId = _buId, role = "Vendedor" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE /api/v1/users/{id}/memberships/{buId} ────────────────────────

    [Fact]
    public async Task RemoveMembership_AsTAdmin_Returns204()
    {
        // Arrange
        var user = User.Activate("user@example.com", "User Test", "uid123", _tenantId, DateTimeOffset.UtcNow);
        user.AssignMembership(_buId, Role.Create("Vendedor"), Guid.NewGuid());

        _factory.UserRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(user);

        _factory.TenantAdminCounter
            .CountActiveTenantAdminsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(2); // Tem outros TAdmins

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.DeleteAsync(
            $"/api/v1/users/{_targetUserId}/memberships/{_buId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("GestorBU")]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task RemoveMembership_AsNonTAdmin_Returns403(string role)
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        // Act
        var response = await client.DeleteAsync(
            $"/api/v1/users/{_targetUserId}/memberships/{_buId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT /api/v1/users/{id}/memberships/{buId} ───────────────────────────

    [Theory]
    [InlineData("GestorBU")]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task ChangeRole_AsNonTAdmin_Returns403(string role)
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/v1/users/{_targetUserId}/memberships/{_buId}",
            new { role = "Vendedor" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeRole_AsTAdmin_Returns200()
    {
        // Arrange
        var user = User.Activate("user@example.com", "User Test", "uid123", _tenantId, DateTimeOffset.UtcNow);
        user.AssignMembership(_buId, Role.Create("Vendedor"), Guid.NewGuid());

        _factory.UserRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(user);

        _factory.TenantAdminCounter
            .CountActiveTenantAdminsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(2);

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/v1/users/{_targetUserId}/memberships/{_buId}",
            new { role = "GestorBU" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
    }
}
