using System.Net;
using System.Text.Json;
using Authentication.Api.Tests.Helpers;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Ports.Results;
using Authentication.Contracts.Errors;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Authentication.Api.Tests.Middleware;

/// <summary>
/// Testes de integração para o <c>AuthenticationMiddleware</c>.
///
/// Verifica:
///   - Token ausente → sem AuthContext → requisição prossegue (rota pública).
///   - Token presente e válido → AuthContext injetado.
///   - Token ausente em rota protegida → 401 AUTH-ERR-001.
///   - Token expirado → 401 AUTH-ERR-003.
///   - Token de tenant divergente → 401 AUTH-ERR-004 (PBT-02).
///   - Usuário inativo → 403 AUTH-ERR-005.
///
/// Mapeia: TASK-16, design.md § 5.4, Req 4, Req 5.4, PBT-02.
/// </summary>
public sealed class AuthenticationMiddlewareTests : IClassFixture<AuthenticationApiFactory>
{
    private readonly AuthenticationApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public AuthenticationMiddlewareTests(AuthenticationApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact(DisplayName = "Token ausente → 401 AUTH-ERR-001 para rota protegida (Req 4.2)")]
    public async Task MissingToken_Returns401_WithAuthErr001()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "token ausente em rota protegida deve retornar 401 (Req 4.2)");

        var body = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(body, JsonOptions);

        error.Should().NotBeNull();
        error!.Code.Should().Be("AUTH-ERR-001");
        error.Message.Should().NotBeNullOrWhiteSpace();
        error.Message.Should().NotContainEquivalentOf("identity_uid",
            because: "resposta de erro nunca expõe identity_uid (DD-001, Req 10.4)");
    }

    [Fact(DisplayName = "Token com assinatura inválida → 401 AUTH-ERR-002 (Req 4.2)")]
    public async Task InvalidSignatureToken_Returns401_WithAuthErr002()
    {
        // Configura IdentityProvider para rejeitar token com código AUTH-ERR-002
        _factory.IdentityProvider
            .VerifyTokenAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new IdentityProviderException("AUTH-ERR-002", "Assinatura inválida."));

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        request.Headers.Add("Authorization", "Bearer invalid.jwt.token");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(body, JsonOptions);
        error!.Code.Should().Be("AUTH-ERR-002");

        // Reset para não afetar outros testes
        _factory.IdentityProvider
            .VerifyTokenAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new VerifyTokenResult
            {
                ProviderUserRef = "ref-default",
                FirebaseTenant = AuthenticationApiFactory.DefaultFirebaseTenant,
                Email = "user@acme.com",
                SignInProvider = "password"
            }));
    }

    [Fact(DisplayName = "Token expirado → 401 AUTH-ERR-003 (Req 4.2)")]
    public async Task ExpiredToken_Returns401_WithAuthErr003()
    {
        _factory.IdentityProvider
            .VerifyTokenAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new IdentityProviderException("AUTH-ERR-003", "Token expirado."));

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        request.Headers.Add("Authorization", "Bearer expired.token.here");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(body, JsonOptions);
        error!.Code.Should().Be("AUTH-ERR-003");

        _factory.IdentityProvider
            .VerifyTokenAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new VerifyTokenResult
            {
                ProviderUserRef = "ref-default",
                FirebaseTenant = AuthenticationApiFactory.DefaultFirebaseTenant,
                Email = "user@acme.com",
                SignInProvider = "password"
            }));
    }

    [Fact(DisplayName = "Token de tenant divergente → 401 AUTH-ERR-004 (PBT-02, Req 4.3)")]
    public async Task CrossTenantToken_Returns401_WithAuthErr004()
    {
        // Token com firebase_tenant diferente do tenant resolvido pelo slug
        _factory.IdentityProvider
            .VerifyTokenAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new VerifyTokenResult
            {
                ProviderUserRef = "ref-tenant-b",
                FirebaseTenant = "firebase-tenant-OUTRO", // tenant diferente do slug "acme"
                Email = "user@outro.com",
                SignInProvider = "password"
            }));

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        request.Headers.Add("Authorization", "Bearer token.tenant.b");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "token de tenant diferente deve resultar em 401 AUTH-ERR-004 (PBT-02)");

        var body = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(body, JsonOptions);
        error!.Code.Should().Be("AUTH-ERR-004",
            because: "tenant divergente usa AUTH-ERR-004 (design.md § 12, PBT-02)");

        // Reset
        _factory.IdentityProvider
            .VerifyTokenAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new VerifyTokenResult
            {
                ProviderUserRef = "ref-default",
                FirebaseTenant = AuthenticationApiFactory.DefaultFirebaseTenant,
                Email = "user@acme.com",
                SignInProvider = "password"
            }));
    }

    [Fact(DisplayName = "Usuário inativo → 403 AUTH-ERR-005 sem expor identity_uid (Req 5.4)")]
    public async Task InactiveUser_Returns403_WithAuthErr005_WithoutIdentityUid()
    {
        // Token válido mas usuário não existe no directory (ativo)
        _factory.IdentityProvider
            .VerifyTokenAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new VerifyTokenResult
            {
                ProviderUserRef = "ref-inactive-user",
                FirebaseTenant = AuthenticationApiFactory.DefaultFirebaseTenant,
                Email = "inactive@acme.com",
                SignInProvider = "password"
            }));

        // UserDirectory retorna null para este usuário (sem user_id ativo)
        _factory.UserDirectory
            .FindUserAsync(
                "ref-inactive-user",
                AuthenticationApiFactory.DefaultTenantId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Authentication.Application.Ports.Results.UserDirectoryResult?>(null));

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        request.Headers.Add("Authorization", "Bearer token.inactive.user");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "usuário sem user_id ativo deve resultar em 403 (Req 5.4)");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContainEquivalentOf("identity_uid",
            because: "resposta nunca expõe identity_uid (DD-001, Req 10.4)");

        var error = JsonSerializer.Deserialize<ErrorResponse>(body, JsonOptions);
        error!.Code.Should().Be("AUTH-ERR-005");

        // Reset
        _factory.IdentityProvider
            .VerifyTokenAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new VerifyTokenResult
            {
                ProviderUserRef = "ref-default",
                FirebaseTenant = AuthenticationApiFactory.DefaultFirebaseTenant,
                Email = "user@acme.com",
                SignInProvider = "password"
            }));
    }

    [Fact(DisplayName = "Resposta de erro de auth não expõe detalhe interno (Req 10.4)")]
    public async Task AuthError_ResponseBody_DoesNotExposeInternalDetail()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        // Sem Authorization header

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContainEquivalentOf("firebase",
            because: "resposta não deve expor detalhe do IdP (Req 10.4)");
        body.Should().NotContainEquivalentOf("identity_uid",
            because: "resposta não deve expor identity_uid (DD-001, Req 10.4)");
        body.Should().NotContainEquivalentOf("exception",
            because: "resposta não deve expor stack trace (Req 10.4)");
    }
}
