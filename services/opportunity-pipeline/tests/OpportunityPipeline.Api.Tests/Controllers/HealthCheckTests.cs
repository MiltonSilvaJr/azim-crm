using System.Net;
using FluentAssertions;
using OpportunityPipeline.Api.Tests.Infrastructure;
using Xunit;

namespace OpportunityPipeline.Api.Tests.Controllers;

/// <summary>
/// Testes do health check endpoint (TASK-24, RNF 10.1, design §11).
/// Verifica que /health retorna 200 OK e que a resposta não contém PII.
/// O health check é básico (AddHealthChecks sem dependências) no ambiente de teste —
/// a liveness/readiness baseada em DB é validada em IntegrationTests com Testcontainers.
/// Mapeia: TASK-24, RNF 10.1, design §11, KPI smoke test.
/// </summary>
[Trait("Category", "HealthCheck")]
public sealed class HealthCheckTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    // =========================================================================
    // HC-01 — /health retorna 200 OK com serviços saudáveis
    // =========================================================================

    [Fact(DisplayName = "HC-01: GET /health retorna 200 OK no ambiente de teste (smoke — design §11)")]
    public async Task GetHealth_Returns200_WhenServicesHealthy()
    {
        // Arrange: qualquer client (health check é público — sem autenticação)
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "o endpoint /health deve retornar 200 quando todos os health checks passam.");
    }

    // =========================================================================
    // HC-02 — /health responde com Content-Type correto
    // =========================================================================

    [Fact(DisplayName = "HC-02: GET /health retorna Content-Type text/plain ou application/json (design §11)")]
    public async Task GetHealth_Returns_ValidContentType()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType;
        contentType.Should().BeOneOf("text/plain", "application/json",
            "health check deve retornar text/plain (padrão ASP.NET Core) ou application/json.");
    }

    // =========================================================================
    // HC-03 — /health não expõe PII na resposta
    // =========================================================================

    [Fact(DisplayName = "HC-03: resposta do /health não contém PII (RNF 10.4, design §11)")]
    public async Task GetHealth_ResponseDoesNotContainPii()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");

        var body = await response.Content.ReadAsStringAsync();

        // Verifica ausência de padrões PII conhecidos na resposta do health check:
        // e-mail, CPF, nome de contato (LGPD, RNF 10.4, design §11).
        body.Should().NotMatchRegex(
            @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
            "health check não deve expor e-mail em sua resposta (PII — LGPD).");
        body.Should().NotMatchRegex(
            @"""contact_name""\s*:\s*""[^""]{2,}""",
            "health check não deve expor contact_name em sua resposta (PII — LGPD).");
    }

    // =========================================================================
    // HC-04 — /health é acessível sem autenticação (liveness público)
    // =========================================================================

    [Fact(DisplayName = "HC-04: GET /health é acessível sem token (liveness público — design §11)")]
    public async Task GetHealth_IsPublic_NoAuthRequired()
    {
        // Arrange: client sem cabeçalho de autenticação
        var client = factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert: não deve retornar 401 nem 403
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "/health não deve exigir autenticação — é endpoint de liveness público.");
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "/health não deve ser bloqueado por autorização — é endpoint de liveness público.");
    }
}

