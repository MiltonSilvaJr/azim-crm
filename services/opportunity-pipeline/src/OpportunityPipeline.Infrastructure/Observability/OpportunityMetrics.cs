using System.Diagnostics.Metrics;

namespace OpportunityPipeline.Infrastructure.Observability;

/// <summary>
/// Métricas do módulo opportunity-pipeline (RNF 10.2, design §11, TASK-24).
/// Instrumentação via System.Diagnostics.Metrics (compatível com OpenTelemetry).
/// Métricas snake_case conforme convenção do projeto.
///
/// Métricas implementadas (6 obrigatórias — design §11):
/// 1. opportunities_created_total
/// 2. opportunities_won_total
/// 3. opportunities_lost_total
/// 4. opportunities_stale_total
/// 5. commission_snapshot_created_total
/// 6. kanban_request_duration_ms (histograma p95)
/// Gauge adicional: outbox_pending_events
///
/// Mapeia: RNF 10.2, design §11, TASK-24.
/// </summary>
public sealed class OpportunityMetrics : IDisposable
{
    /// <summary>Nome do Meter — usado para registro no DI e em testes de smoke.</summary>
    public const string MeterName = "OpportunityPipeline";

    private readonly Meter _meter;

    // =========================================================================
    // Contadores (design §11)
    // =========================================================================

    /// <summary>Total de oportunidades criadas por tenant/BU (design §11).</summary>
    private readonly Counter<long> _opportunitiesCreated;

    /// <summary>Total de oportunidades ganhas (com snapshot) por tenant/BU (design §11).</summary>
    private readonly Counter<long> _opportunitiesWon;

    /// <summary>Total de oportunidades perdidas por tenant/BU (design §11).</summary>
    private readonly Counter<long> _opportunitiesLost;

    /// <summary>Total de oportunidades marcadas como estagnadas (design §11).</summary>
    private readonly Counter<long> _opportunitiesStale;

    /// <summary>Total de snapshots imutáveis criados (design §11, RNF 5).</summary>
    private readonly Counter<long> _commissionSnapshotCreated;

    // =========================================================================
    // Histograma (design §11 — p95 ≤ 2.000 ms)
    // =========================================================================

    /// <summary>Duração das requisições Kanban em ms (histograma para p95 — RNF 1, design §11).</summary>
    private readonly Histogram<double> _kanbanRequestDuration;

    // =========================================================================
    // Gauge (Outbox — TRD §17, design §11)
    // =========================================================================

    /// <summary>Gauge de eventos pendentes no Outbox (alerta se > 100 por > 5 min — design §11).</summary>
    private readonly ObservableGauge<long> _outboxPendingEvents;
    private long _currentOutboxPending;

    // =========================================================================
    // Construtor
    // =========================================================================

    public OpportunityMetrics()
    {
        _meter = new Meter(MeterName, version: "1.0.0");

        _opportunitiesCreated = _meter.CreateCounter<long>(
            "opportunities_created_total",
            unit: "{oportunidade}",
            description: "Total de oportunidades criadas (Req 1, design §11)");

        _opportunitiesWon = _meter.CreateCounter<long>(
            "opportunities_won_total",
            unit: "{oportunidade}",
            description: "Total de oportunidades ganhas com snapshot de comissão (Req 14, design §11)");

        _opportunitiesLost = _meter.CreateCounter<long>(
            "opportunities_lost_total",
            unit: "{oportunidade}",
            description: "Total de oportunidades perdidas (Req 10, design §11)");

        _opportunitiesStale = _meter.CreateCounter<long>(
            "opportunities_stale_total",
            unit: "{oportunidade}",
            description: "Total de oportunidades marcadas como estagnadas (Req 17, design §11)");

        _commissionSnapshotCreated = _meter.CreateCounter<long>(
            "commission_snapshot_created_total",
            unit: "{snapshot}",
            description: "Total de snapshots imutáveis de comissão criados (Req 14, RNF 5, design §11)");

        _kanbanRequestDuration = _meter.CreateHistogram<double>(
            "kanban_request_duration_ms",
            unit: "ms",
            description: "Duração das requisições Kanban em ms — p95 ≤ 2.000 ms (RNF 1, design §11)");

        _outboxPendingEvents = _meter.CreateObservableGauge<long>(
            "outbox_pending_events",
            () => _currentOutboxPending,
            unit: "{evento}",
            description: "Número de eventos pendentes no Outbox (alerta se > 100 por > 5 min — TRD §17, design §11)");
    }

    // =========================================================================
    // Métodos de registro de métricas
    // =========================================================================

    /// <summary>Registra criação de oportunidade (chamado por CreateOpportunityHandler).</summary>
    public void RecordOpportunityCreated(string tenantId, string buId) =>
        _opportunitiesCreated.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId),
            new KeyValuePair<string, object?>("bu_id", buId));

    /// <summary>Registra oportunidade ganha com snapshot (chamado por WinOpportunityHandler).</summary>
    public void RecordOpportunityWon(string tenantId) =>
        _opportunitiesWon.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId));

    /// <summary>Registra oportunidade perdida (chamado por LoseOpportunityHandler).</summary>
    public void RecordOpportunityLost(string tenantId) =>
        _opportunitiesLost.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId));

    /// <summary>Registra oportunidade estagnada (chamado por StagnationDetectionService).</summary>
    public void RecordOpportunityStale(string tenantId) =>
        _opportunitiesStale.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId));

    /// <summary>Registra snapshot imutável criado (chamado junto com RecordOpportunityWon).</summary>
    public void RecordCommissionSnapshotCreated(string tenantId) =>
        _commissionSnapshotCreated.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId));

    /// <summary>Registra duração de uma requisição Kanban em ms (histograma p95 — RNF 1).</summary>
    public void RecordKanbanRequestDuration(double durationMs, string tenantId, string buId) =>
        _kanbanRequestDuration.Record(durationMs,
            new KeyValuePair<string, object?>("tenant_id", tenantId),
            new KeyValuePair<string, object?>("bu_id", buId));

    /// <summary>
    /// Atualiza o número de eventos pendentes no Outbox.
    /// Chamado pelo OutboxPublisher em cada ciclo de publicação.
    /// Alerta se > 100 por > 5 min (TRD §17, design §11).
    /// </summary>
    public void UpdateOutboxPendingEvents(long count) =>
        Interlocked.Exchange(ref _currentOutboxPending, count);

    public void Dispose() => _meter.Dispose();
}
