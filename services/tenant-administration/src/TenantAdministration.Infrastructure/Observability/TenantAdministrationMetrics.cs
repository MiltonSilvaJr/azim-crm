using System.Diagnostics.Metrics;

namespace TenantAdministration.Infrastructure.Observability;

/// <summary>
/// Métricas instrumentadas do módulo Tenant Administration (TASK-22, design.md §11, RNF 6.3).
/// Utiliza <see cref="System.Diagnostics.Metrics.Meter"/> (OpenTelemetry-compatível).
/// Nomes em snake_case conforme convenção do projeto.
///
/// Métricas expostas:
/// - <c>tenant_provisioned_total</c>: provisionamentos bem-sucedidos
/// - <c>tenant_provisioning_failed_total</c>: falhas de provisionamento
/// - <c>tenant_branding_update_total</c>: atualizações de branding
/// - <c>tenant_wcag_reject_total</c>: rejeições de contraste WCAG
/// - <c>tenant_provisioning_duration_seconds</c>: duração do provisionamento (histograma)
/// - <c>cdn_invalidation_total</c>: invalidações de CDN disparadas
/// - <c>outbox_failed_total</c>: eventos do Outbox marcados como failed
/// </summary>
public sealed class TenantAdministrationMetrics : IDisposable
{
    /// <summary>Nome do meter conforme convenção do módulo.</summary>
    public const string MeterName = "TenantAdministration";

    private readonly Meter _meter;

    /// <summary>Contagem de provisionamentos bem-sucedidos.</summary>
    public Counter<long> ProvisionedTotal { get; }

    /// <summary>Contagem de falhas de provisionamento.</summary>
    public Counter<long> ProvisioningFailedTotal { get; }

    /// <summary>Contagem de atualizações de branding.</summary>
    public Counter<long> BrandingUpdateTotal { get; }

    /// <summary>Contagem de rejeições de contraste WCAG.</summary>
    public Counter<long> WcagRejectTotal { get; }

    /// <summary>Duração do provisionamento em segundos (histograma).</summary>
    public Histogram<double> ProvisioningDurationSeconds { get; }

    /// <summary>Contagem de invalidações de CDN.</summary>
    public Counter<long> CdnInvalidationTotal { get; }

    /// <summary>Contagem de eventos do Outbox marcados como failed (alerta crítico).</summary>
    public Counter<long> OutboxFailedTotal { get; }

    /// <param name="meterFactory">Factory de Meter do .NET runtime (injetada via DI).</param>
    public TenantAdministrationMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        ProvisionedTotal = _meter.CreateCounter<long>(
            name: "tenant_provisioned_total",
            description: "Número de tenants provisionados com sucesso.");

        ProvisioningFailedTotal = _meter.CreateCounter<long>(
            name: "tenant_provisioning_failed_total",
            description: "Número de falhas no provisionamento de tenant.");

        BrandingUpdateTotal = _meter.CreateCounter<long>(
            name: "tenant_branding_update_total",
            description: "Número de atualizações de branding realizadas.");

        WcagRejectTotal = _meter.CreateCounter<long>(
            name: "tenant_wcag_reject_total",
            description: "Número de rejeições de contraste WCAG AA insuficiente.");

        ProvisioningDurationSeconds = _meter.CreateHistogram<double>(
            name: "tenant_provisioning_duration_seconds",
            unit: "s",
            description: "Duração do provisionamento de tenant em segundos.");

        CdnInvalidationTotal = _meter.CreateCounter<long>(
            name: "cdn_invalidation_total",
            description: "Número de invalidações de CDN disparadas após BrandingChanged.");

        OutboxFailedTotal = _meter.CreateCounter<long>(
            name: "outbox_failed_total",
            description: "Número de eventos do Outbox marcados como failed após MaxRetries.");
    }

    /// <inheritdoc/>
    public void Dispose() => _meter.Dispose();
}
