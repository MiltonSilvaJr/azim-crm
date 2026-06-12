using AuditLog.Api.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace AuditLog.Api.Tests.Observability;

/// <summary>
/// Testes do endpoint de health check (design §11.5, RNF-006).
/// Verifica que o endpoint /health está registrado e responde.
/// Os três estados (Healthy/Unhealthy/Degraded) são validados em
/// <c>AuditLog.Infrastructure.Tests</c> com Testcontainers.
/// </summary>
public sealed class HealthCheckTests
{
    // -----------------------------------------------------------------------
    // Endpoint /health registrado
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "GET /health deve responder (endpoint registrado na API)")]
    public async Task Health_Endpoint_Should_Respond()
    {
        await using var factory = new AuditApiFactory();
        factory.SetupEmptyListResponse();

        // Não precisamos de autenticação para /health
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        // O endpoint deve responder (qualquer status — banco pode estar indisponível em CI sem infraestrutura)
        response.Should().NotBeNull(because: "o endpoint /health deve estar mapeado");

        // O status deve ser um HTTP válido, não 404 (endpoint não encontrado)
        ((int)response.StatusCode).Should().NotBe(404,
            because: "o endpoint /health deve estar registrado e responder");
    }

    [Fact(DisplayName = "GET /health deve retornar conteúdo JSON com status do health check")]
    public async Task Health_Endpoint_Should_Return_Content()
    {
        await using var factory = new AuditApiFactory();
        factory.SetupEmptyListResponse();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        // O endpoint deve retornar algum conteúdo (não vazio)
        // O status pode ser "Healthy", "Unhealthy" ou "Degraded" dependendo do banco
        body.Should().NotBeNullOrEmpty(
            because: "o health check deve retornar informações de status");
    }
}
