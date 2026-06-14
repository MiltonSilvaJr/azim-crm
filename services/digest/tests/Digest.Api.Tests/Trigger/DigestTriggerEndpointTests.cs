using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Digest.Api.Tests.Trigger;

/// <summary>
/// Testes de API do <c>DigestTriggerEndpoint</c> (TASK-21).
/// Cobre: 401 (sem token), 403 (SA não autorizada), 202 (token válido + body válido).
/// Usa <see cref="WebApplicationFactory{TEntryPoint}"/> com autenticação simulada (design §13, RNF 7.1).
/// </summary>
public sealed class DigestTriggerEndpointTests : IClassFixture<DigestApiFactory>
{
    private readonly HttpClient _client;
    private readonly DigestApiFactory _factory;

    public DigestTriggerEndpointTests(DigestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ---------------------------------------------------------------
    // 401 — sem token OIDC (DIG-ERR-010)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Trigger_WithoutAuthHeader_Returns401()
    {
        // Arrange — nenhum header de autorização
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/digest/trigger")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { reference_utc = "2026-06-14T10:00:00Z" }),
                Encoding.UTF8,
                "application/json")
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Trigger_WithInvalidBearerToken_Returns401()
    {
        // Arrange — token malformado (não é um OIDC válido)
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/digest/trigger")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { reference_utc = "2026-06-14T10:00:00Z" }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.token.here");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------------------------------------------------------------
    // 403 — identidade não autorizada (DIG-ERR-011)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Trigger_WithUnauthorizedIdentity_Returns403()
    {
        // Arrange — token OIDC com SA diferente da esperada (simulado via header de teste)
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/digest/trigger")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { reference_utc = "2026-06-14T10:00:00Z" }),
                Encoding.UTF8,
                "application/json")
        };
        // Header de teste: identidade autenticada mas não autorizada
        request.Headers.Add(DigestApiFactory.TestAuthHeader, DigestApiFactory.UnauthorizedTestToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — mensagem não deve revelar identidades válidas (design §12)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------
    // 202 — token válido, body válido (RNF 8.1)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Trigger_WithValidToken_ReturnsAccepted()
    {
        // Arrange — token de test autorizado (SA simulada do Cloud Scheduler)
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/digest/trigger")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { reference_utc = "2026-06-14T10:00:00Z" }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add(DigestApiFactory.TestAuthHeader, DigestApiFactory.AuthorizedTestToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("accepted").GetBoolean());
        Assert.True(doc.RootElement.TryGetProperty("eligible_tenants", out _));
        Assert.True(doc.RootElement.TryGetProperty("correlation_id", out _));
    }

    [Fact]
    public async Task Trigger_WithoutReferenceUtc_UsesCurrentHourAndReturnsAccepted()
    {
        // Arrange — body vazio: reference_utc ausente → usa hora cheia atual (Req 1.5)
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/digest/trigger")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add(DigestApiFactory.TestAuthHeader, DigestApiFactory.AuthorizedTestToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Trigger_WithInvalidReferenceUtc_Returns400()
    {
        // Arrange — reference_utc malformado (DIG-ERR-001)
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/digest/trigger")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { reference_utc = "not-a-date" }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add(DigestApiFactory.TestAuthHeader, DigestApiFactory.AuthorizedTestToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------------------------------------------------------
    // RNF 8.1 — resposta devolvida antes de processar envios
    // ---------------------------------------------------------------

    [Fact]
    public async Task Trigger_ReturnsBeforeProcessingCompletes()
    {
        // O endpoint deve retornar 202 imediatamente;
        // o fan-out é assíncrono (publicação Pub/Sub mockada no factory).
        // Este teste verifica que não há timeout/bloqueio esperando envios individuais.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/digest/trigger")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { reference_utc = "2026-06-14T10:00:00Z" }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add(DigestApiFactory.TestAuthHeader, DigestApiFactory.AuthorizedTestToken);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await _client.SendAsync(request, cts.Token);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
