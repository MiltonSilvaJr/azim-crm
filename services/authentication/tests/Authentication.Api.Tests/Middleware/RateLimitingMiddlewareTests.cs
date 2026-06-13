using System.Net;
using System.Text.Json;
using Authentication.Api.Tests.Helpers;
using Authentication.Contracts.Errors;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using Xunit;

namespace Authentication.Api.Tests.Middleware;

/// <summary>
/// Testes de integração para o <c>RateLimitingMiddleware</c>.
///
/// Verifica:
///   - Requisição dentro do limite → prossegue (não gera 429).
///   - Excesso de tentativas → 429 AUTH-ERR-040 sem revelar existência de conta.
///   - Resposta 429 não expõe detalhe interno (IP, tenant_id, stack trace).
///   - Ordem do middleware: Rate Limiting ocorre ANTES de Authentication.
///
/// Mapeia: TASK-16, design.md § 5.4, RNF 8, Req 10.5.
/// </summary>
public sealed class RateLimitingMiddlewareTests : IClassFixture<AuthenticationApiFactory>
{
    private readonly AuthenticationApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public RateLimitingMiddlewareTests(AuthenticationApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact(DisplayName = "Rate limit excedido → 429 AUTH-ERR-040 (RNF 8, Req 10.5)")]
    public async Task RateLimitExceeded_Returns429_WithAuthErr040()
    {
        // Configura RateLimiter para bloquear todas as requisições
        _factory.RateLimiter
            .IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            because: "excesso de requisições deve retornar 429 (RNF 8, Req 10.5)");

        var body = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(body, JsonOptions);

        error.Should().NotBeNull();
        error!.Code.Should().Be("AUTH-ERR-040",
            because: "rate limit usa código AUTH-ERR-040 (design.md § 12)");
        error.Message.Should().NotBeNullOrWhiteSpace();

        // Reset
        _factory.RateLimiter
            .IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
    }

    [Fact(DisplayName = "Resposta 429 não expõe IP, tenant_id, stack trace (Req 10.4)")]
    public async Task RateLimitResponse_DoesNotExposeInternalDetail()
    {
        _factory.RateLimiter
            .IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContainEquivalentOf("exception",
            because: "resposta de 429 não deve expor stack trace (Req 10.4)");
        body.Should().NotContainEquivalentOf("tenant_id",
            because: "resposta de 429 não deve expor tenant_id (Req 10.4)");
        body.Should().NotContainEquivalentOf("identity_uid",
            because: "resposta de 429 não deve expor identity_uid (DD-001, Req 10.4)");
        body.Should().NotContainEquivalentOf("firebase",
            because: "resposta de 429 não deve expor detalhe do IdP (Req 10.4)");

        // Reset
        _factory.RateLimiter
            .IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
    }

    [Fact(DisplayName = "Rate limit dentro do limite → prossegue sem 429 (RNF 8)")]
    public async Task WithinRateLimit_DoesNotReturn429()
    {
        // RateLimiter padrão: permite todas (configurado na factory)
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
            because: "requisição dentro do limite não deve gerar 429 (RNF 8)");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("AUTH-ERR-040",
            because: "sem excesso de rate limit não deve haver AUTH-ERR-040");
    }

    [Fact(DisplayName = "Rate limit ocorre ANTES de autenticação — 429 retornado mesmo sem token (design.md § 5.4)")]
    public async Task RateLimitMiddleware_ExecutesBefore_AuthMiddleware()
    {
        // Sem token e com rate limit bloqueado → deve retornar 429 (não 401)
        // Isso prova que RateLimitingMiddleware vem antes de AuthenticationMiddleware no pipeline
        _factory.RateLimiter
            .IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        // Sem Authorization header — sem token

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            because: "RateLimitingMiddleware precede AuthenticationMiddleware " +
                     "no pipeline (design.md § 5.4) e bloqueia antes de auth validar o token");

        // Reset
        _factory.RateLimiter
            .IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
    }
}
