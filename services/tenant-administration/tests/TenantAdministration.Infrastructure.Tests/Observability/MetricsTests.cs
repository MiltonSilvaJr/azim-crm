using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TenantAdministration.Infrastructure.Identity;
using TenantAdministration.Infrastructure.Observability;
using TenantAdministration.Infrastructure.Outbox;
using TenantAdministration.Infrastructure.Persistence.Repositories;
using TenantAdministration.Infrastructure.Saga;
using TenantAdministration.Infrastructure.Tests.Fixtures;
using Xunit;

namespace TenantAdministration.Infrastructure.Tests.Observability;

/// <summary>
/// Testes de métricas do módulo Tenant Administration (TASK-22, design.md §11, RNF 6.3).
/// Verifica que contadores são incrementados nos cenários corretos.
/// </summary>
[Collection("Postgres")]
public sealed class MetricsTests
{
    private readonly PostgresContainerFixture _fixture;

    public MetricsTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "tenant_provisioning_failed_total incrementa após falha no IdP")]
    public async Task ProvisioningFailed_Total_Increments_OnIdpFailure()
    {
        // Arrange
        var factory = new FakeMeterFactory();
        var metrics = new TenantAdministrationMetrics(factory);

        long failedCount = 0;

        // Listener de métricas para capturar o counter
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "tenant_provisioning_failed_total")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
        {
            Interlocked.Add(ref failedCount, measurement);
        });
        listener.Start();

        var idp = new FakeIdentityTenantProvisioner { ShouldFailOnCreate = true };
        var tenantCtx = new FakeTenantContext();
        using var db = _fixture.CreateDbContext();
        var outbox = new OutboxRepository(db, tenantCtx);
        var clock = new Infrastructure.Clock.SystemClock();
        var saga = new TenantProvisioningSaga(
            db, idp, outbox, tenantCtx, clock,
            NullLogger<TenantProvisioningSaga>.Instance,
            metrics);

        // Act
        try
        {
            await saga.ExecuteAsync(
                Guid.NewGuid(),
                slug: "metrics-fail-test",
                displayName: "Metrics Fail Test",
                timezone: "America/Sao_Paulo",
                digestTime: "07:00",
                adminEmail: "admin@metrics.com",
                idempotencyKey: Guid.NewGuid().ToString());
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("TA-ERR-009"))
        {
            // esperado — falha no IdP
        }

        // Assert
        failedCount.Should().Be(1,
            "tenant_provisioning_failed_total deve incrementar 1 após falha no IdP (TASK-22, design.md §11).");
    }

    [Fact(DisplayName = "tenant_provisioned_total incrementa após provisionamento bem-sucedido")]
    public async Task ProvisionedTotal_Increments_OnSuccess()
    {
        // Arrange
        var factory = new FakeMeterFactory();
        var metrics = new TenantAdministrationMetrics(factory);

        long provisionedCount = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "tenant_provisioned_total")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
        {
            Interlocked.Add(ref provisionedCount, measurement);
        });
        listener.Start();

        var idp = new FakeIdentityTenantProvisioner();
        var tenantCtx = new FakeTenantContext();
        using var db = _fixture.CreateDbContext();
        var outbox = new OutboxRepository(db, tenantCtx);
        var clock = new Infrastructure.Clock.SystemClock();
        var saga = new TenantProvisioningSaga(
            db, idp, outbox, tenantCtx, clock,
            NullLogger<TenantProvisioningSaga>.Instance,
            metrics);

        // Act
        await saga.ExecuteAsync(
            Guid.NewGuid(),
            slug: "metrics-ok-test",
            displayName: "Metrics OK Test",
            timezone: "America/Sao_Paulo",
            digestTime: "07:00",
            adminEmail: "admin@metrics-ok.com",
            idempotencyKey: Guid.NewGuid().ToString());

        // Assert
        provisionedCount.Should().Be(1,
            "tenant_provisioned_total deve incrementar 1 após provisionamento bem-sucedido (TASK-22).");
    }

    [Fact(DisplayName = "TenantAdministrationMetrics expõe todos os contadores do design.md §11")]
    public void Metrics_ExposeAllRequiredCounters()
    {
        // Arrange
        using var factory = new FakeMeterFactory();
        using var metrics = new TenantAdministrationMetrics(factory);

        // Assert — verificar que todos os contadores definidos no design.md §11 existem
        metrics.ProvisionedTotal.Should().NotBeNull("tenant_provisioned_total deve existir");
        metrics.ProvisioningFailedTotal.Should().NotBeNull("tenant_provisioning_failed_total deve existir");
        metrics.BrandingUpdateTotal.Should().NotBeNull("tenant_branding_update_total deve existir");
        metrics.WcagRejectTotal.Should().NotBeNull("tenant_wcag_reject_total deve existir");
        metrics.ProvisioningDurationSeconds.Should().NotBeNull("tenant_provisioning_duration_seconds deve existir");
        metrics.CdnInvalidationTotal.Should().NotBeNull("cdn_invalidation_total deve existir");
        metrics.OutboxFailedTotal.Should().NotBeNull("outbox_failed_total deve existir");

        // Nomes snake_case (convenção do projeto)
        metrics.ProvisionedTotal.Name.Should().Be("tenant_provisioned_total");
        metrics.ProvisioningFailedTotal.Name.Should().Be("tenant_provisioning_failed_total");
        metrics.BrandingUpdateTotal.Name.Should().Be("tenant_branding_update_total");
        metrics.WcagRejectTotal.Name.Should().Be("tenant_wcag_reject_total");
        metrics.ProvisioningDurationSeconds.Name.Should().Be("tenant_provisioning_duration_seconds");
        metrics.CdnInvalidationTotal.Name.Should().Be("cdn_invalidation_total");
        metrics.OutboxFailedTotal.Name.Should().Be("outbox_failed_total");
    }
}
