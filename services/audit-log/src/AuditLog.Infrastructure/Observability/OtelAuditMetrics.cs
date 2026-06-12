using System.Diagnostics;
using System.Diagnostics.Metrics;
using AuditLog.Application.Abstractions;

namespace AuditLog.Infrastructure.Observability;

/// <summary>
/// Implementação concreta de <see cref="IAuditMetrics"/> usando <c>System.Diagnostics.Metrics</c>
/// (OpenTelemetry SDK coleta via <c>MeterProvider</c>).
/// <para>
/// As 5 métricas do design §11.2 são registradas aqui:
/// <list type="bullet">
/// <item><c>audit_events_received_total</c> — Counter (RNF-004.1)</item>
/// <item><c>audit_insert_failures_total</c> — Counter (RNF-004.1)</item>
/// <item><c>audit_insert_latency_seconds</c> — Histogram (RNF-003)</item>
/// <item><c>audit_query_without_tenant_context_total</c> — Counter (DD-007)</item>
/// <item><c>audit_pii_masking_applied_total</c> — Counter (REQ-004)</item>
/// </list>
/// </para>
/// <para>Nenhuma label ou atributo de métrica contém PII (RNF-002).</para>
/// </summary>
public sealed class OtelAuditMetrics : IAuditMetrics, IDisposable
{
    /// <summary>Nome do Meter; usado pelo OpenTelemetry para filtrar instrumentos.</summary>
    public const string MeterName = "AuditLog";

    private readonly Meter _meter;
    private readonly Counter<long> _eventsReceived;
    private readonly Counter<long> _insertFailures;
    private readonly Histogram<double> _insertLatency;
    private readonly Counter<long> _queryWithoutTenant;
    private readonly Counter<long> _piiMaskingApplied;

    /// <summary>
    /// Inicializa o <see cref="OtelAuditMetrics"/> criando um <see cref="Meter"/> com os
    /// instrumentos configurados conforme design §11.2.
    /// </summary>
    public OtelAuditMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        _eventsReceived = _meter.CreateCounter<long>(
            name: "audit_events_received_total",
            description: "Número total de eventos de auditoria recebidos pelo AuditService (RNF-004.1).");

        _insertFailures = _meter.CreateCounter<long>(
            name: "audit_insert_failures_total",
            description: "Número total de falhas de INSERT em audit_logs (RNF-004.1).");

        _insertLatency = _meter.CreateHistogram<double>(
            name: "audit_insert_latency_seconds",
            unit: "s",
            description: "Latência de INSERT em audit_logs em segundos (RNF-003, SLO p95 ≤ 500 ms).");

        _queryWithoutTenant = _meter.CreateCounter<long>(
            name: "audit_query_without_tenant_context_total",
            description: "Número de consultas bloqueadas por ausência de tenant_id no contexto (DD-007).");

        _piiMaskingApplied = _meter.CreateCounter<long>(
            name: "audit_pii_masking_applied_total",
            description: "Número de operações em que o PiiMasker aplicou mascaramento (REQ-004).");
    }

    /// <inheritdoc/>
    public void IncrementEventsReceived() => _eventsReceived.Add(1);

    /// <inheritdoc/>
    public void IncrementInsertFailures() => _insertFailures.Add(1);

    /// <inheritdoc/>
    public void IncrementQueryWithoutTenantContext() => _queryWithoutTenant.Add(1);

    /// <inheritdoc/>
    public void RecordInsertLatency(double seconds) => _insertLatency.Record(seconds);

    /// <inheritdoc/>
    public void IncrementPiiMaskingApplied() => _piiMaskingApplied.Add(1);

    /// <inheritdoc/>
    public void Dispose() => _meter.Dispose();
}
