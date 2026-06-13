using System.Net;
using System.Text.Json;
using Authentication.Api.Tests.Helpers;
using Authentication.Contracts.Dtos;
using Authentication.Contracts.Errors;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using Xunit;

namespace Authentication.Api.Tests.Controllers;

/// <summary>
/// Testes de integração para <c>AuthController</c>.
///
/// Verifica:
///   - GET /v1/auth/me com token válido → 200 MeResponse sem identity_uid.
///   - GET /v1/auth/me sem token → 401 AUTH-ERR-001.
///   - POST /v1/auth/logout com token válido → 200 LogoutResponse.
///   - POST /v1/auth/logout sem token → 401.
///   - PBT-04: N≥3 chamadas consecutivas de logout retornam 200 (idempotência).
///
/// Mapeia: TASK-18, design.md § 8.1, § 8.2, Req 9, Req 11, PBT-04.
/// </summary>
public sealed class AuthControllerTests : IClassFixture<AuthenticationApiFactory>
{
    private readonly AuthenticationApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public AuthControllerTests(AuthenticationApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // =========================================================================
    // GET /v1/auth/me
    // =========================================================================

    [Fact(DisplayName = "GET /v1/auth/me com token válido → 200 MeResponse sem identity_uid (Req 11)")]
    public async Task GetMe_WithValidToken_Returns200_WithMeResponse()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        request.Headers.Add("Authorization", "Bearer valid.token.here");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "token válido com usuário ativo deve retornar 200 (Req 11)");

        var body = await response.Content.ReadAsStringAsync();

        // Nunca expõe identity_uid (DD-001, Req 11.4)
        body.Should().NotContainEquivalentOf("identity_uid",
            because: "MeResponse nunca deve expor identity_uid (DD-001, Req 11.4)");
        body.Should().NotContainEquivalentOf("provider_user_ref",
            because: "MeResponse nunca deve expor referência ao Identity Provider (DD-001)");
        body.Should().NotContainEquivalentOf("firebase",
            because: "MeResponse nunca deve expor detalhe do IdP (DD-001)");

        var me = JsonSerializer.Deserialize<MeResponse>(body, JsonOptions);
        me.Should().NotBeNull();
        me!.UserId.Should().Be(AuthenticationApiFactory.DefaultUserId,
            because: "user_id deve corresponder ao usuário resolvido pelo UserDirectory");
        me.Email.Should().Be("user@acme.com",
            because: "email deve vir do UserDirectory");
        me.TenantId.Should().Be(AuthenticationApiFactory.DefaultTenantId,
            because: "tenant_id deve corresponder ao tenant do slug (Req 11.1)");
    }

    [Fact(DisplayName = "GET /v1/auth/me sem token → 401 AUTH-ERR-001 (Req 4.2)")]
    public async Task GetMe_WithoutToken_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        // Sem Authorization header

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "sem token em rota protegida deve retornar 401 (Req 4.2)");

        var body = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(body, JsonOptions);

        error.Should().NotBeNull();
        error!.Code.Should().Be("AUTH-ERR-001");
    }

    [Fact(DisplayName = "GET /v1/auth/me resposta não expõe firebase nem identity_uid (DD-001, Req 11.4)")]
    public async Task GetMe_Response_NeverExposesInternalIdentifiers()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        request.Headers.Add("Authorization", "Bearer valid.token.here");

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Garantias de não exposição (DD-001, Req 10.4, Req 11.4)
        body.Should().NotContainEquivalentOf("identity_uid");
        body.Should().NotContainEquivalentOf("firebase");
        body.Should().NotContainEquivalentOf("exception");
    }

    // =========================================================================
    // POST /v1/auth/logout
    // =========================================================================

    [Fact(DisplayName = "POST /v1/auth/logout com token válido → 200 LogoutResponse (Req 9)")]
    public async Task Logout_WithValidToken_Returns200_WithRevokedStatus()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/logout");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        request.Headers.Add("Authorization", "Bearer valid.token.here");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "logout com token válido deve retornar 200 (Req 9)");

        var body = await response.Content.ReadAsStringAsync();
        var logoutResp = JsonSerializer.Deserialize<LogoutResponse>(body, JsonOptions);

        logoutResp.Should().NotBeNull();
        logoutResp!.Status.Should().Be("revoked",
            because: "logout sempre retorna status 'revoked' em sucesso (Req 9, design.md § 8.2)");
    }

    [Fact(DisplayName = "POST /v1/auth/logout sem token → 401 (Req 4.2)")]
    public async Task Logout_WithoutToken_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/logout");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        // Sem Authorization header

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "logout sem token deve retornar 401 (Req 4.2)");
    }

    // =========================================================================
    // PBT-04 — Idempotência de logout: N≥3 chamadas consecutivas retornam 200
    // =========================================================================

    /// <summary>
    /// Invólucro para N no intervalo [3, 8] (PBT-04).
    /// </summary>
    public sealed record LogoutCallCount(int Value);

    /// <summary>Gerador de LogoutCallCount com N entre 3 e 8.</summary>
    public static class Pbt04Generators
    {
        public static Arbitrary<LogoutCallCount> LogoutCallCountArbitrary() =>
            ArbMap.Default
                .GeneratorFor<int>()
                .Select(n => ((n % 6) + 6) % 6 + 3) // mapeia para [3, 8]
                .Select(n => new LogoutCallCount(n))
                .ToArbitrary();
    }

    /// <summary>
    /// PBT-04: N chamadas consecutivas de logout com token válido devem todas retornar 200.
    ///
    /// Verifica que a idempotência de revogação de sessão é preservada em nível de API:
    /// chamar /logout N vezes produz sempre 200 sem erro (Req 9.5, design.md § 6.5).
    ///
    /// Mapeia: PBT-04, Req 9.5, design.md § 8.2.
    /// </summary>
    [Property(
        MaxTest = 20,
        Arbitrary = [typeof(Pbt04Generators)],
        DisplayName = "PBT-04: N≥3 chamadas consecutivas de logout sempre retornam 200 (Req 9.5)")]
    public bool LogoutIsIdempotent_ForNConsecutiveCalls(LogoutCallCount callCount)
    {
        var n = callCount.Value;

        for (var i = 0; i < n; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/logout");
            request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
            request.Headers.Add("Authorization", "Bearer valid.token.here");

            var response = _client.SendAsync(request).GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var logoutResp = JsonSerializer.Deserialize<LogoutResponse>(body, JsonOptions);

            if (response.StatusCode != HttpStatusCode.OK) return false;
            if (logoutResp?.Status != "revoked") return false;
        }

        return true;
    }

    /// <summary>
    /// PBT-04 complementar: quando o IdP sinaliza "já revogado" (AUTH-ERR-004),
    /// a API ainda retorna 200 (idempotência capturada no SessionRevocationService).
    ///
    /// Mapeia: PBT-04, Req 9.5, design.md § 6.5.
    /// </summary>
    [Fact(DisplayName = "PBT-04: logout com 'já revogado' no IdP ainda retorna 200 (Req 9.5)")]
    public async Task Logout_WhenAlreadyRevoked_StillReturns200()
    {
        // Simula IdP sinalizando "já revogado" (AUTH-ERR-004 = sessão já inválida)
        _factory.IdentityProvider.RevokeRefreshTokensAsync(
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(
                new Authentication.Application.Ports.Exceptions.IdentityProviderException(
                    "AUTH-ERR-004",
                    "Sessão já revogada.")));

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/logout");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        request.Headers.Add("Authorization", "Bearer valid.token.here");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "'já revogado' é tratado como sucesso idempotente (PBT-04, Req 9.5)");

        var body = await response.Content.ReadAsStringAsync();
        var logoutResp = JsonSerializer.Deserialize<LogoutResponse>(body, JsonOptions);
        logoutResp!.Status.Should().Be("revoked");

        // Reset
        _factory.IdentityProvider.RevokeRefreshTokensAsync(
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }
}
