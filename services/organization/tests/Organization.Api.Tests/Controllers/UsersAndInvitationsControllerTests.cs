using FluentAssertions;
using NSubstitute;
using Organization.Api.Tests.Infrastructure;
using Organization.Domain.Aggregates;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Organization.Api.Tests.Controllers;

/// <summary>
/// Testes de integração para UsersController e InvitationsController.
/// Verifica: RBAC, anti-enumeração, aceite público (sem JWT), paginação.
/// </summary>
public sealed class UsersAndInvitationsControllerTests : IAsyncDisposable
{
    private readonly OrganizationApiFactory _factory;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _buId = Guid.NewGuid();

    public UsersAndInvitationsControllerTests()
    {
        _factory = new OrganizationApiFactory();
        _factory.Clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        _factory.TokenHasher.GenerateToken().Returns(("plain-token", "hash-value"));
        _factory.TokenHasher.Hash(Arg.Any<string>()).Returns("hash-value");
    }

    // ── GET /api/v1/users ────────────────────────────────────────────────────

    [Fact]
    public async Task ListUsers_AsTAdmin_Returns200()
    {
        // Arrange
        _factory.UserRepository
            .ListActiveAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<User>());

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.GetAsync("/api/v1/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("GestorBU")]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task ListUsers_AsNonTAdmin_Returns403(string role)
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        // Act
        var response = await client.GetAsync("/api/v1/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListUsers_WithoutAuth_Returns401()
    {
        // Arrange
        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.GetAsync("/api/v1/users");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    // ── DELETE /api/v1/users/{id} ────────────────────────────────────────────

    [Fact]
    public async Task DeactivateUser_AsTAdmin_Returns204()
    {
        // Arrange
        var user = User.Activate("user@example.com", "User Test", "uid123", _tenantId, DateTimeOffset.UtcNow);
        _factory.UserRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(user);

        _factory.TenantAdminCounter
            .CountActiveTenantAdminsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(2); // Tem outros TAdmins

        _factory.ActivityCounter
            .CountFutureAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(0); // Sem atividades futuras

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.DeleteAsync($"/api/v1/users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("GestorBU")]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task DeactivateUser_AsNonTAdmin_Returns403(string role)
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        // Act
        var response = await client.DeleteAsync($"/api/v1/users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── POST /api/v1/users/invite ────────────────────────────────────────────

    [Fact]
    public async Task InviteUser_AsTAdmin_Returns202()
    {
        // Arrange
        // Handler verifica IsEmailActiveUserAsync no InvitationRepository (não UserRepository)
        _factory.InvitationRepository
            .IsEmailActiveUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false); // E-mail não pertence a usuário ativo

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        var request = new
        {
            email = "novo@example.com",
            memberships = new[] { new { buId = _buId, role = "Vendedor" } },
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/users/invite", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Theory]
    [InlineData("GestorBU")]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task InviteUser_AsNonTAdmin_Returns403(string role)
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        var request = new
        {
            email = "novo@example.com",
            memberships = new[] { new { buId = _buId, role = "Vendedor" } },
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/users/invite", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Anti-enumeração: POST /api/v1/users/invite ───────────────────────────

    [Fact]
    public async Task InviteUser_ActiveEmail_ReturnsGenericConflict_NoAccountReveal()
    {
        // Arrange — e-mail ativo no sistema (ORG-ERR-003)
        // Handler chama IsEmailActiveUserAsync no InvitationRepository (não UserRepository)
        _factory.InvitationRepository
            .IsEmailActiveUserAsync("ativo@example.com", Arg.Any<CancellationToken>())
            .Returns(true);

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        var request = new
        {
            email = "ativo@example.com",
            memberships = new[] { new { buId = _buId, role = "Vendedor" } },
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/users/invite", request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert — deve retornar 409 com mensagem genérica (sem revelar existência de conta)
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body.Should().Contain("ORG-ERR-003");
        body.Should().NotContain("ativo@example.com", because: "PII não deve aparecer no erro");
        body.Should().NotContain("Usuário já existe", because: "Não pode revelar existência de conta");
    }

    // ── POST /api/v1/invitations/accept ─────────────────────────────────────

    [Fact]
    public async Task AcceptInvitation_ValidToken_Returns200()
    {
        // Arrange — endpoint PÚBLICO, sem JWT
        var tokenHash = Domain.ValueObjects.InvitationToken.FromHash("valid-hash");
        var invitation = UserInvitation.Create(
            "novo@example.com",
            tokenHash,
            _tenantId,
            new List<(Guid BuId, Domain.ValueObjects.Role Role)>
            {
                (_buId, Domain.ValueObjects.Role.Create("Vendedor")),
            },
            DateTimeOffset.UtcNow.AddHours(72),
            DateTimeOffset.UtcNow);

        _factory.InvitationRepository
            .GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(invitation);

        _factory.TokenHasher
            .Hash("valid-plain-token")
            .Returns("valid-hash");

        _factory.IdentityProvisioner
            .ProvisionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("new-uid-123");

        _factory.UserRepository
            .GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var client = _factory.CreateAnonymousClient(); // Público — sem JWT

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new { token = "valid-plain-token", displayName = "Novo Usuário" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("valid-plain-token", because: "Token não deve aparecer na resposta");
    }

    [Fact]
    public async Task AcceptInvitation_InvalidToken_Returns400_Generic()
    {
        // Arrange — token inválido (anti-enumeração: mesma resposta para inexistente/expirado)
        _factory.InvitationRepository
            .GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((UserInvitation?)null);

        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new { token = "invalid-token", displayName = "Usuário" });

        // Assert — mensagem genérica, sem revelar motivo exato
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ORG-ERR-004");
        body.Should().NotContain("invalid-token", because: "Token não deve aparecer no erro");
    }

    // ── POST /api/v1/invitations/{id}/revoke ────────────────────────────────

    [Fact]
    public async Task RevokeInvitation_AsTAdmin_Returns204()
    {
        // Arrange
        var tokenHash = Domain.ValueObjects.InvitationToken.FromHash("hash123");
        var invitation = UserInvitation.Create(
            "pendente@example.com",
            tokenHash,
            _tenantId,
            new List<(Guid, Domain.ValueObjects.Role)>
            {
                (_buId, Domain.ValueObjects.Role.Create("Vendedor")),
            },
            DateTimeOffset.UtcNow.AddHours(72),
            DateTimeOffset.UtcNow);

        _factory.InvitationRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(invitation);

        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, "TAdmin");

        // Act
        var response = await client.PostAsync(
            $"/api/v1/invitations/{Guid.NewGuid()}/revoke",
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("GestorBU")]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public async Task RevokeInvitation_AsNonTAdmin_Returns403(string role)
    {
        // Arrange
        var client = _factory.CreateClientAs(_tenantId, _userId, _buId, role);

        // Act
        var response = await client.PostAsync(
            $"/api/v1/invitations/{Guid.NewGuid()}/revoke",
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
    }
}
