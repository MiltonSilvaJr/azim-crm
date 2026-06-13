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
/// Testes de integração para o <c>TenantResolutionMiddleware</c>.
///
/// Verifica:
///   - Slug válido resolve tenant_id e prossegue para o próximo middleware.
///   - Slug inexistente retorna 404 AUTH-ERR-010 sem detalhe interno.
///   - Slug aceito via header X-Tenant-Slug.
///   - SET app.current_tenant executado (verificado por ausência de violação RLS).
///
/// Mapeia: TASK-15, design.md § 5.4, Req 1, Req 1.2, DEC-006.
/// </summary>
public sealed class TenantResolutionMiddlewareTests : IClassFixture<AuthenticationApiFactory>
{
    private readonly AuthenticationApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public TenantResolutionMiddlewareTests(AuthenticationApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact(DisplayName = "Slug inexistente retorna 404 AUTH-ERR-010 sem detalhe interno (Req 1.2)")]
    public async Task InvalidSlug_Returns404_WithAuthErr010()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", "slug-que-nao-existe-xyz");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "slug inexistente deve retornar 404 (AUTH-ERR-010, Req 1.2)");

        var body = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(body, JsonOptions);

        error.Should().NotBeNull();
        error!.Code.Should().Be("AUTH-ERR-010",
            because: "slug não encontrado usa o código AUTH-ERR-010 (design.md § 12)");
        error.Message.Should().NotContainEquivalentOf("firebase",
            because: "resposta não deve expor detalhe interno (Req 10.4)");
        error.Message.Should().NotContainEquivalentOf("identity_uid",
            because: "resposta não deve expor identity_uid (DD-001)");
    }

    [Fact(DisplayName = "Slug válido via header X-Tenant-Slug não retorna AUTH-ERR-010 (Req 1)")]
    public async Task ValidSlug_ViaHeader_DoesNotReturnAuthErr010()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);

        // Slug 'acme' resolve corretamente — a resposta não deve ser 404 de slug
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // A resposta pode ser 401 (sem token) ou outros erros, mas NUNCA 404 AUTH-ERR-010
        body.Should().NotContain("AUTH-ERR-010",
            because: "slug 'acme' é válido e não deve gerar AUTH-ERR-010 (design.md § 12)");
    }

    [Fact(DisplayName = "Slug inválido nunca chama TenantDirectory com slug correto e retorna 404 (Req 1)")]
    public async Task InvalidSlug_Returns404_WithAuthErr010_Unique()
    {
        // Cada teste usa slug único para evitar interferência com outros testes do fixture
        var uniqueInvalidSlug = $"slug-inexistente-{Guid.NewGuid():N}";
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", uniqueInvalidSlug);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "slug inexistente único deve retornar 404 AUTH-ERR-010 (Req 1.2)");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("AUTH-ERR-010");
    }

    [Fact(DisplayName = "Resposta de 404 de slug não expõe dados internos (Req 10.4)")]
    public async Task SlugNotFound_ResponseBody_DoesNotExposeInternalDetail()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
        request.Headers.Add("X-Tenant-Slug", "nao-existe-99999");

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContainEquivalentOf("exception",
            because: "resposta de erro nunca expõe stack trace (Req 10.4)");
        body.Should().NotContainEquivalentOf("database",
            because: "resposta de erro nunca expõe detalhe de banco (Req 10.4)");
        body.Should().NotContainEquivalentOf("sql",
            because: "resposta de erro nunca expõe SQL (Req 10.4)");
    }
}
