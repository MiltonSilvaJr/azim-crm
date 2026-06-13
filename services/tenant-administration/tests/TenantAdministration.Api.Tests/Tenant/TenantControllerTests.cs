using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using TenantAdministration.Api.Tests.Infrastructure;
using TenantAdministration.Contracts.Errors;
using TenantAdministration.Contracts.Tenant;
using Xunit;

namespace TenantAdministration.Api.Tests.Tenant;

/// <summary>
/// Testes de contrato para os endpoints do plano de tenant (TASK-18).
/// design.md §8.2 — GET /api/v1/tenant, PATCH /api/v1/tenant,
///                   GET /api/v1/tenant/branding, PUT /api/v1/tenant/branding.
/// </summary>
public sealed class TenantControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    public TenantControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.TenantRepository.ClearReceivedCalls();
    }

    // ─── GET /api/v1/tenant ────────────────────────────────────────────────────

    [Fact(DisplayName = "GET /api/v1/tenant com TenantAdmin retorna 200 com dados do tenant")]
    public async Task GetCurrentTenant_TenantAdmin_Returns200()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateProvisionedTenant(tenantId, "vellus");
        _factory.TenantRepository
            .FindByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var client = _factory.CreateTenantAdminClient(tenantId);

        // Act
        var response = await client.GetAsync("/api/v1/tenant");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TenantResponse>(JsonOpts);
        body.Should().NotBeNull();
        body!.TenantId.Should().Be(tenantId);
        body.Slug.Should().Be("vellus");
        body.Status.Should().Be("provisioned");
    }

    [Fact(DisplayName = "GET /api/v1/tenant nunca expõe adminEmail na resposta")]
    public async Task GetCurrentTenant_ResponseDoesNotExposeAdminEmail()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateProvisionedTenant(tenantId, "vellus");
        _factory.TenantRepository
            .FindByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var client = _factory.CreateTenantAdminClient(tenantId);

        // Act
        var response = await client.GetAsync("/api/v1/tenant");
        var bodyString = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        bodyString.Should().NotContain("adminEmail");
        bodyString.Should().NotContain("admin@vellus.com.br");
        bodyString.Should().NotContain("identityTenantId");
    }

    [Fact(DisplayName = "GET /api/v1/tenant por PlatformOperator retorna 403")]
    public async Task GetCurrentTenant_PlatformOperator_Returns403()
    {
        // Arrange
        var client = _factory.CreatePlatformOperatorClient();

        // Act
        var response = await client.GetAsync("/api/v1/tenant");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "GET /api/v1/tenant com tenant inexistente retorna 404 com TA-ERR-008")]
    public async Task GetCurrentTenant_TenantNotFound_Returns404()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _factory.TenantRepository
            .FindByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Domain.Aggregates.Tenant?)null);

        var client = _factory.CreateTenantAdminClient(tenantId);

        // Act
        var response = await client.GetAsync("/api/v1/tenant");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(TaErrorCodes.TenantNotFound);
    }

    // ─── PATCH /api/v1/tenant ─────────────────────────────────────────────────

    [Fact(DisplayName = "PATCH /api/v1/tenant com slug no body retorna 422 com TA-ERR-011")]
    public async Task UpdateDigestConfig_SlugInBody_Returns422WithTaErr011()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateProvisionedTenant(tenantId, "vellus");
        _factory.TenantRepository
            .FindByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var client = _factory.CreateTenantAdminClient(tenantId);
        var request = new UpdateDigestConfigRequest(
            Timezone: "America/Sao_Paulo",
            DigestTime: "08:00",
            Slug: "novo-slug");  // proibido — TA-ERR-011

        // Act
        var response = await client.PatchAsJsonAsync("/api/v1/tenant", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(TaErrorCodes.SlugImmutable);
    }

    [Fact(DisplayName = "PATCH /api/v1/tenant por PlatformOperator retorna 403")]
    public async Task UpdateDigestConfig_PlatformOperator_Returns403()
    {
        // Arrange
        var client = _factory.CreatePlatformOperatorClient();
        var request = new UpdateDigestConfigRequest(
            Timezone: "America/Sao_Paulo",
            DigestTime: "08:00");

        // Act
        var response = await client.PatchAsJsonAsync("/api/v1/tenant", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── GET /api/v1/tenant/branding ──────────────────────────────────────────

    [Fact(DisplayName = "GET /api/v1/tenant/branding retorna 200 com branding do tenant")]
    public async Task GetBranding_TenantAdmin_Returns200()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateProvisionedTenant(tenantId, "vellus");
        _factory.TenantRepository
            .FindByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var client = _factory.CreateTenantAdminClient(tenantId);

        // Act
        var response = await client.GetAsync("/api/v1/tenant/branding");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BrandingResponse>(JsonOpts);
        body.Should().NotBeNull();
        body!.TenantId.Should().Be(tenantId);
    }

    [Fact(DisplayName = "GET /api/v1/tenant/branding de tenant de outro tenant retorna 404 (isolamento)")]
    public async Task GetBranding_OtherTenantId_Returns404()
    {
        // Arrange — tenant A faz request mas o contexto tem o ID do tenant B
        var tenantIdA = Guid.NewGuid();
        var tenantIdB = Guid.NewGuid();

        // Simula que o tenant B não é encontrado quando tenant A tenta acessar
        _factory.TenantRepository
            .FindByIdAsync(tenantIdB, Arg.Any<CancellationToken>())
            .Returns((Domain.Aggregates.Tenant?)null);

        // Cliente autentica como Admin do tenant A, mas X-Test-TenantId aponta para B
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "TenantAdmin");
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantIdB.ToString());

        // Act
        var response = await client.GetAsync("/api/v1/tenant/branding");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(TaErrorCodes.TenantNotFound);
    }

    [Fact(DisplayName = "GET /api/v1/tenant/branding por PlatformOperator retorna 403")]
    public async Task GetBranding_PlatformOperator_Returns403()
    {
        // Arrange
        var client = _factory.CreatePlatformOperatorClient();

        // Act
        var response = await client.GetAsync("/api/v1/tenant/branding");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static Domain.Aggregates.Tenant CreateProvisionedTenant(Guid id, string slug)
    {
        var slugVo = Domain.ValueObjects.Slug.Create(slug).Value;
        var timezone = Domain.ValueObjects.TimezoneIana.Create("America/Sao_Paulo").Value;
        var digestTime = Domain.ValueObjects.DigestTime.Create("07:00").Value;
        var tenant = Domain.Aggregates.Tenant.Provision(
            slugVo, "Vellus", timezone, digestTime, "admin@vellus.com.br", DateTimeOffset.UtcNow);
        SetId(tenant, id);
        return tenant;
    }

    private static void SetId(Domain.Aggregates.Tenant tenant, Guid id)
    {
        var prop = typeof(Domain.Aggregates.Tenant)
            .GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(tenant, id);
    }
}
