using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TenantAdministration.Api.Tests.Infrastructure;
using TenantAdministration.Application.Commands;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;
using TenantAdministration.Contracts.Errors;
using TenantAdministration.Contracts.Platform;
using Xunit;

namespace TenantAdministration.Api.Tests.Platform;

/// <summary>
/// Testes de contrato para os endpoints do plano de plataforma (TASK-17).
/// design.md §8.1 — TASK-17: provision, suspend, reactivate.
/// </summary>
public sealed class PlatformTenantControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    public PlatformTenantControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        // Reset mocks a cada construção para evitar estado entre testes
        _factory.TenantRepository.ClearReceivedCalls();
        _factory.ProvisioningSaga.ClearReceivedCalls();
    }

    // ─── POST /api/v1/platform/tenants ────────────────────────────────────────

    [Fact(DisplayName = "POST /platform/tenants com body válido retorna 201 com tenantId e slug")]
    public async Task Provision_ValidRequest_Returns201WithTenantIdAndSlug()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _factory.ProvisioningSaga
            .ExecuteAsync(
                Arg.Any<Guid>(), "vellus", Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProvisioningResult(tenantId, "idp-tenant-123"));

        _factory.TenantRepository
            .ExistsSlugAsync("vellus", Arg.Any<CancellationToken>())
            .Returns(false);

        var client = _factory.CreatePlatformOperatorClient();
        var request = new ProvisionTenantRequest(
            Slug: "vellus",
            SlugConfirmation: "vellus",
            DisplayName: "Vellus",
            Timezone: "America/Sao_Paulo",
            DigestTime: "07:00",
            AdminEmail: "admin@vellus.com.br");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/platform/tenants", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ProvisionTenantResponse>(JsonOpts);
        body.Should().NotBeNull();
        body!.Slug.Should().Be("vellus");
        body.Status.Should().Be("provisioned");
        body.TenantId.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "POST /platform/tenants com slug duplicado retorna 409 com TA-ERR-002")]
    public async Task Provision_DuplicateSlug_Returns409WithTaErr002()
    {
        // Arrange
        _factory.TenantRepository
            .ExistsSlugAsync("existing-slug", Arg.Any<CancellationToken>())
            .Returns(true);

        var client = _factory.CreatePlatformOperatorClient();
        var request = new ProvisionTenantRequest(
            Slug: "existing-slug",
            SlugConfirmation: "existing-slug",
            DisplayName: "Existing",
            Timezone: "America/Sao_Paulo",
            DigestTime: "07:00",
            AdminEmail: "admin@existing.com");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/platform/tenants", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(TaErrorCodes.SlugAlreadyInUse);
    }

    [Fact(DisplayName = "POST /platform/tenants com slugConfirmation divergente retorna 422 com TA-ERR-003")]
    public async Task Provision_SlugConfirmationMismatch_Returns422WithTaErr003()
    {
        // Arrange
        var client = _factory.CreatePlatformOperatorClient();
        var request = new ProvisionTenantRequest(
            Slug: "vellus",
            SlugConfirmation: "velus",  // diverge
            DisplayName: "Vellus",
            Timezone: "America/Sao_Paulo",
            DigestTime: "07:00",
            AdminEmail: "admin@vellus.com.br");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/platform/tenants", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(TaErrorCodes.SlugConfirmationMismatch);
    }

    [Fact(DisplayName = "POST /platform/tenants com slug inválido retorna 400 com TA-ERR-005")]
    public async Task Provision_InvalidSlugFormat_Returns400WithTaErr005()
    {
        // Arrange
        _factory.TenantRepository
            .ExistsSlugAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var client = _factory.CreatePlatformOperatorClient();
        var request = new ProvisionTenantRequest(
            Slug: "Invalid Slug!",
            SlugConfirmation: "Invalid Slug!",
            DisplayName: "Invalid",
            Timezone: "America/Sao_Paulo",
            DigestTime: "07:00",
            AdminEmail: "admin@invalid.com");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/platform/tenants", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(TaErrorCodes.SlugInvalidFormat);
    }

    [Fact(DisplayName = "POST /platform/tenants por TenantAdmin retorna 403")]
    public async Task Provision_TenantAdminRole_Returns403()
    {
        // Arrange
        var client = _factory.CreateTenantAdminClient(Guid.NewGuid());
        var request = new ProvisionTenantRequest(
            Slug: "vellus",
            SlugConfirmation: "vellus",
            DisplayName: "Vellus",
            Timezone: "America/Sao_Paulo",
            DigestTime: "07:00",
            AdminEmail: "admin@vellus.com.br");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/platform/tenants", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST /platform/tenants lê Idempotency-Key do header e repassa ao command")]
    public async Task Provision_WithIdempotencyKey_PassesKeyToCommand()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString("N");

        _factory.ProvisioningSaga
            .ExecuteAsync(
                Arg.Any<Guid>(), "vellus", Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), idempotencyKey, Arg.Any<CancellationToken>())
            .Returns(new ProvisioningResult(tenantId, "idp-tenant-123"));

        _factory.TenantRepository
            .ExistsSlugAsync("vellus", Arg.Any<CancellationToken>())
            .Returns(false);

        var client = _factory.CreatePlatformOperatorClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        var request = new ProvisionTenantRequest(
            Slug: "vellus",
            SlugConfirmation: "vellus",
            DisplayName: "Vellus",
            Timezone: "America/Sao_Paulo",
            DigestTime: "07:00",
            AdminEmail: "admin@vellus.com.br");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/platform/tenants", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        await _factory.ProvisioningSaga.Received(1)
            .ExecuteAsync(
                Arg.Any<Guid>(), "vellus", Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), idempotencyKey, Arg.Any<CancellationToken>());
    }

    // ─── POST /api/v1/platform/tenants/{id}/suspend ───────────────────────────

    [Fact(DisplayName = "POST /platform/tenants/{id}/suspend em tenant suspenso retorna 409 com TA-ERR-007")]
    public async Task Suspend_AlreadySuspended_Returns409WithTaErr007()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateSuspendedTenant(tenantId);
        _factory.TenantRepository
            .FindByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var client = _factory.CreatePlatformOperatorClient();

        // Act
        var response = await client.PostAsync(
            $"/api/v1/platform/tenants/{tenantId}/suspend", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(TaErrorCodes.InvalidStateTransition);
    }

    [Fact(DisplayName = "POST /platform/tenants/{id}/suspend em tenant inexistente retorna 404 com TA-ERR-008")]
    public async Task Suspend_TenantNotFound_Returns404WithTaErr008()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _factory.TenantRepository
            .FindByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Domain.Aggregates.Tenant?)null);

        var client = _factory.CreatePlatformOperatorClient();

        // Act
        var response = await client.PostAsync(
            $"/api/v1/platform/tenants/{tenantId}/suspend", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(TaErrorCodes.TenantNotFound);
    }

    [Fact(DisplayName = "POST /platform/tenants/{id}/suspend em tenant ativo retorna 200 com status suspended")]
    public async Task Suspend_ActiveTenant_Returns200WithSuspendedStatus()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = CreateProvisionedTenant(tenantId);
        _factory.TenantRepository
            .FindByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var client = _factory.CreatePlatformOperatorClient();

        // Act
        var response = await client.PostAsync(
            $"/api/v1/platform/tenants/{tenantId}/suspend", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TenantStateResponse>(JsonOpts);
        body!.Status.Should().Be("suspended");
    }

    [Fact(DisplayName = "POST /platform/tenants/{id}/reactivate por TenantAdmin retorna 403")]
    public async Task Reactivate_TenantAdminRole_Returns403()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var client = _factory.CreateTenantAdminClient(tenantId);

        // Act
        var response = await client.PostAsync(
            $"/api/v1/platform/tenants/{tenantId}/reactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Resposta de provision não expõe adminEmail")]
    public async Task Provision_SuccessResponse_DoesNotExposeAdminEmail()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _factory.ProvisioningSaga
            .ExecuteAsync(
                Arg.Any<Guid>(), "vellus", Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProvisioningResult(tenantId, "idp-tenant-123"));

        _factory.TenantRepository
            .ExistsSlugAsync("vellus", Arg.Any<CancellationToken>())
            .Returns(false);

        var client = _factory.CreatePlatformOperatorClient();
        var request = new ProvisionTenantRequest(
            Slug: "vellus",
            SlugConfirmation: "vellus",
            DisplayName: "Vellus",
            Timezone: "America/Sao_Paulo",
            DigestTime: "07:00",
            AdminEmail: "admin@vellus.com.br");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/platform/tenants", request);
        var bodyString = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        bodyString.Should().NotContain("adminEmail");
        bodyString.Should().NotContain("admin@vellus.com.br");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static Domain.Aggregates.Tenant CreateProvisionedTenant(Guid id)
    {
        var slug = Domain.ValueObjects.Slug.Create("vellus").Value;
        var timezone = Domain.ValueObjects.TimezoneIana.Create("America/Sao_Paulo").Value;
        var digestTime = Domain.ValueObjects.DigestTime.Create("07:00").Value;
        var tenant = Domain.Aggregates.Tenant.Provision(
            slug, "Vellus", timezone, digestTime, "admin@vellus.com.br", DateTimeOffset.UtcNow);
        // forçar ID via reflection para controle no teste
        SetId(tenant, id);
        return tenant;
    }

    private static Domain.Aggregates.Tenant CreateSuspendedTenant(Guid id)
    {
        var tenant = CreateProvisionedTenant(id);
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
