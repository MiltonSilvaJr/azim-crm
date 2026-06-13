using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using TenantAdministration.Api.Tests.Infrastructure;
using TenantAdministration.Contracts.Errors;
using TenantAdministration.Contracts.Public;
using Xunit;

namespace TenantAdministration.Api.Tests.Public;

/// <summary>
/// Testes de contrato para o endpoint público brand.json (TASK-19).
/// design.md §8.3 — GET /brand/{slug}/brand.json.
/// Anti-enumeração: slug inexistente = tenant suspenso = mesmo 404 TA-ERR-008.
/// Cache-Control: public, max-age=300. ETag baseado em versão. 304 Not Modified.
/// </summary>
public sealed class BrandControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    public BrandControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.TenantRepository.ClearReceivedCalls();
    }

    // ─── GET /brand/{slug}/brand.json ─────────────────────────────────────────

    [Fact(DisplayName = "GET /brand/{slug}/brand.json de tenant ativo retorna 200 com brand.json")]
    public async Task GetBrandJson_ActiveTenant_Returns200()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateProvisionedTenant(tenantId, "vellus");
        _factory.TenantRepository
            .FindBySlugAsync("vellus", Arg.Any<CancellationToken>())
            .Returns(tenant);

        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.GetAsync("/brand/vellus/brand.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BrandJsonResponse>(JsonOpts);
        body.Should().NotBeNull();
        body!.Slug.Should().Be("vellus");
    }

    [Fact(DisplayName = "GET /brand/{slug}/brand.json retorna Cache-Control: public, max-age=300")]
    public async Task GetBrandJson_Returns_CacheControlHeader()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateProvisionedTenant(tenantId, "vellus-beta");
        _factory.TenantRepository
            .FindBySlugAsync("vellus-beta", Arg.Any<CancellationToken>())
            .Returns(tenant);

        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.GetAsync("/brand/vellus-beta/brand.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromSeconds(300));
        response.Headers.CacheControl.Public.Should().BeTrue();
    }

    [Fact(DisplayName = "GET /brand/{slug}/brand.json retorna ETag no response")]
    public async Task GetBrandJson_Returns_ETagHeader()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateProvisionedTenant(tenantId, "vellus-gamma");
        _factory.TenantRepository
            .FindBySlugAsync("vellus-gamma", Arg.Any<CancellationToken>())
            .Returns(tenant);

        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.GetAsync("/brand/vellus-gamma/brand.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();
        response.Headers.ETag!.Tag.Should().StartWith("\"");
        response.Headers.ETag.Tag.Should().EndWith("\"");
    }

    [Fact(DisplayName = "GET /brand/{slug}/brand.json com If-None-Match igual ao ETag retorna 304")]
    public async Task GetBrandJson_IfNoneMatchEqualETag_Returns304()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateProvisionedTenant(tenantId, "vellus-delta");
        _factory.TenantRepository
            .FindBySlugAsync("vellus-delta", Arg.Any<CancellationToken>())
            .Returns(tenant);

        var client = _factory.CreateAnonymousClient();

        // Obtém ETag do primeiro request
        var firstResponse = await client.GetAsync("/brand/vellus-delta/brand.json");
        var eTag = firstResponse.Headers.ETag?.Tag;
        eTag.Should().NotBeNullOrEmpty();

        // Act — segundo request com If-None-Match
        var request = new HttpRequestMessage(HttpMethod.Get, "/brand/vellus-delta/brand.json");
        request.Headers.Add("If-None-Match", eTag);
        var secondResponse = await client.SendAsync(request);

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Fact(DisplayName = "GET /brand/{slug}/brand.json de slug inexistente retorna 404 com TA-ERR-008")]
    public async Task GetBrandJson_NonExistentSlug_Returns404WithTaErr008()
    {
        // Arrange
        _factory.TenantRepository
            .FindBySlugAsync("slug-inexistente", Arg.Any<CancellationToken>())
            .Returns((Domain.Aggregates.Tenant?)null);

        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.GetAsync("/brand/slug-inexistente/brand.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(TaErrorCodes.TenantNotFound);
    }

    [Fact(DisplayName = "GET /brand/{slug}/brand.json de tenant suspenso retorna 404 (anti-enumeração)")]
    public async Task GetBrandJson_SuspendedTenant_Returns404SameAsNonExistent()
    {
        // Arrange — tenant suspenso deve retornar MESMO 404 que slug inexistente (RNF 1)
        var tenantId = Guid.NewGuid();
        var tenant = CreateSuspendedTenant(tenantId, "tenant-suspenso");
        _factory.TenantRepository
            .FindBySlugAsync("tenant-suspenso", Arg.Any<CancellationToken>())
            .Returns(tenant);

        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.GetAsync("/brand/tenant-suspenso/brand.json");

        // Assert — anti-enumeração: suspenso = inexistente = mesmo 404
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(TaErrorCodes.TenantNotFound);
    }

    [Fact(DisplayName = "GET /brand/{slug}/brand.json não expõe adminEmail nem dados sensíveis")]
    public async Task GetBrandJson_DoesNotExposeAdminEmail()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateProvisionedTenant(tenantId, "vellus-epsilon");
        _factory.TenantRepository
            .FindBySlugAsync("vellus-epsilon", Arg.Any<CancellationToken>())
            .Returns(tenant);

        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.GetAsync("/brand/vellus-epsilon/brand.json");
        var bodyString = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        bodyString.Should().NotContain("adminEmail");
        bodyString.Should().NotContain("admin@vellus.com.br");
        bodyString.Should().NotContain("identityTenantId");
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

    private static Domain.Aggregates.Tenant CreateSuspendedTenant(Guid id, string slug)
    {
        var tenant = CreateProvisionedTenant(id, slug);
        tenant.Suspend(DateTimeOffset.UtcNow);
        return tenant;
    }

    private static void SetId(Domain.Aggregates.Tenant tenant, Guid id)
    {
        var prop = typeof(Domain.Aggregates.Tenant)
            .GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(tenant, id);
    }
}
