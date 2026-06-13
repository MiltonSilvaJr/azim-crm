using System.Net;
using FluentAssertions;
using TenantAdministration.Api.Tests.Infrastructure;
using Xunit;

namespace TenantAdministration.Api.Tests.Security;

/// <summary>
/// Testes de segurança: Platform Operator não pode acessar dados comerciais de tenant (TASK-21).
/// design.md §10, §14, RNF 7, DD-001.
/// Gate: [Trait("Category","SecurityGate")].
/// </summary>
[Trait("Category", "SecurityGate")]
public sealed class PlatOpBlockTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PlatOpBlockTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "PlatOp autenticado em GET /api/v1/tenant recebe 403")]
    public async Task PlatOp_GetTenant_Returns403()
    {
        // Arrange
        var client = _factory.CreatePlatformOperatorClient();

        // Act
        var response = await client.GetAsync("/api/v1/tenant");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Platform Operator não deve acessar dados comerciais de tenant (RNF 7, DD-001).");
    }

    [Fact(DisplayName = "PlatOp autenticado em GET /api/v1/tenant/branding recebe 403")]
    public async Task PlatOp_GetTenantBranding_Returns403()
    {
        // Arrange
        var client = _factory.CreatePlatformOperatorClient();

        // Act
        var response = await client.GetAsync("/api/v1/tenant/branding");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Platform Operator não deve acessar branding de tenant (RNF 7, DD-001).");
    }

    [Fact(DisplayName = "PlatOp autenticado em PATCH /api/v1/tenant recebe 403")]
    public async Task PlatOp_PatchTenant_Returns403()
    {
        // Arrange
        var client = _factory.CreatePlatformOperatorClient();
        var json = System.Text.Json.JsonSerializer.Serialize(
            new { timezone = "America/Sao_Paulo", digestTime = "07:00" });
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await client.PatchAsync("/api/v1/tenant", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Platform Operator não pode atualizar configurações de tenant (RNF 7).");
    }

    [Fact(DisplayName = "PlatOp autenticado em PUT /api/v1/tenant/branding recebe 403")]
    public async Task PlatOp_PutTenantBranding_Returns403()
    {
        // Arrange
        var client = _factory.CreatePlatformOperatorClient();
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("#1A73E8"), "primaryColor");
        content.Add(new StringContent("#34A853"), "secondaryColor");

        // Act
        var response = await client.PutAsync("/api/v1/tenant/branding", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Platform Operator não pode alterar branding de tenant (RNF 7).");
    }
}
