using System.Net;
using System.Text.Json;
using FluentAssertions;
using TenantAdministration.Api.Tests.Infrastructure;
using Xunit;

namespace TenantAdministration.Api.Tests.Contracts;

/// <summary>
/// Testes de contrato OpenAPI (TASK-20).
/// Verifica que o documento OpenAPI gerado pela API contém os endpoints e schemas
/// obrigatórios definidos em design.md §8 (API contracts).
/// </summary>
public sealed class OpenApiContractTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public OpenApiContractTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "GET /openapi/v1.json retorna 200 com documento OpenAPI válido")]
    public async Task OpenApi_Returns200WithValidDocument()
    {
        // Arrange — OpenAPI está disponível em todos os ambientes via MapOpenApi()
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotBeNullOrEmpty();

        var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("openapi", out _)
            .Should().BeTrue(because: "documento OpenAPI deve ter campo 'openapi'");
        doc.RootElement.TryGetProperty("paths", out _)
            .Should().BeTrue(because: "documento OpenAPI deve ter campo 'paths'");
    }

    [Fact(DisplayName = "OpenAPI contém path POST /api/v1/platform/tenants (provision)")]
    public async Task OpenApi_ContainsPlatformTenantProvisionPath()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();

        json.Should().Contain("/api/v1/platform/tenants",
            because: "endpoint de provisionamento deve estar no contrato OpenAPI");
    }

    [Fact(DisplayName = "OpenAPI contém path GET /api/v1/tenant (get current tenant)")]
    public async Task OpenApi_ContainsTenantGetPath()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();

        json.Should().Contain("/api/v1/tenant",
            because: "endpoint de leitura do tenant deve estar no contrato OpenAPI");
    }

    [Fact(DisplayName = "OpenAPI contém path GET /brand/{slug}/brand.json (public brand)")]
    public async Task OpenApi_ContainsPublicBrandPath()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();

        json.Should().Contain("brand.json",
            because: "endpoint público brand.json deve estar no contrato OpenAPI");
    }

    [Fact(DisplayName = "OpenAPI contém path GET /api/v1/tenant/branding (get branding)")]
    public async Task OpenApi_ContainsTenantBrandingPath()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();

        json.Should().Contain("/api/v1/tenant/branding",
            because: "endpoint de branding do tenant deve estar no contrato OpenAPI");
    }

    [Fact(DisplayName = "OpenAPI contém path POST /api/v1/platform/tenants/{tenantId}/suspend")]
    public async Task OpenApi_ContainsSuspendPath()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();

        json.Should().Contain("suspend",
            because: "endpoint de suspensão deve estar no contrato OpenAPI");
    }
}
