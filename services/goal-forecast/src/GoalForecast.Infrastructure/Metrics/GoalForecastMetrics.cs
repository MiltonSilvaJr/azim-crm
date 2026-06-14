using System.Diagnostics;
using System.Diagnostics.Metrics;
using GoalForecast.Application.Ports;

namespace GoalForecast.Infrastructure.Metrics;

/// <summary>
/// Centraliza as métricas e o ActivitySource do módulo goal-forecast.
///
/// Métricas de negócio (RNF 7.2):
/// <list type="bullet">
///   <item><c>goals_created_total</c> — contador de metas criadas por tenant/bu.</item>
///   <item><c>goals_updated_total</c> — contador de metas atualizadas por tenant/bu.</item>
///   <item><c>forecast_panel_requests_total</c> — total de requisições ao painel comparativo.</item>
///   <item><c>forecast_panel_latency_ms</c> — histograma de latência do painel (p95 ≤ 3.000 ms, RNF 3).</item>
/// </list>
///
/// Métricas de circuit breaker (RNF 7.2, DD-007):
/// <list type="bullet">
///   <item><c>pipeline_reader_failures_total</c> — falhas na leitura do pipeline (timeout/erro).</item>
///   <item><c>pipeline_circuit_open_total</c> — aberturas do circuit breaker do pipeline.</item>
/// </list>
///
/// Todos os nomes de métrica em snake_case (convenção OpenTelemetry / Prometheus).
/// Nenhum campo de métrica ou atributo de span expõe valorMeta, PAN, PII ou segredo (RNF-7.3).
///
/// Mapeia: TASK-28, RNF 7.2, design §11.
/// </summary>
public sealed class GoalForecastMetrics : IGoalForecastMetrics, IDisposable
{
    // ── Constantes de nomes (snake_case — Prometheus / OpenTelemetry) ─────────
    /// <summary>Nome do Meter do módulo.</summary>
    public const string MeterName = "GoalForecast";

    /// <summary>Nome do ActivitySource para traces distribuídos.</summary>
    public const string ActivitySourceName = "GoalForecast";

    /// <summary>Contador de metas criadas.</summary>
    public const string GoalsCreatedTotalName = "goals_created_total";

    /// <summary>Contador de metas atualizadas.</summary>
    public const string GoalsUpdatedTotalName = "goals_updated_total";

    /// <summary>Contador de falhas na leitura do pipeline.</summary>
    public const string PipelineReaderFailuresTotalName = "pipeline_reader_failures_total";

    /// <summary>Contador de aberturas do circuit breaker do pipeline.</summary>
    public const string PipelineCircuitOpenTotalName = "pipeline_circuit_open_total";

    /// <summary>Contador de requisições ao painel comparativo.</summary>
    public const string ForecastPanelRequestsTotalName = "forecast_panel_requests_total";

    /// <summary>Histograma de latência do painel (ms).</summary>
    public const string ForecastPanelLatencyMsName = "forecast_panel_latency_ms";

    // ── ActivitySource (compartilhado via singleton estático) ─────────────────
    /// <summary>ActivitySource para spans de command/query (traces distribuídos).</summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "0.1.0");

    // ── Instrumentos do Meter ─────────────────────────────────────────────────
    private readonly Meter _meter;
    private readonly Counter<long> _goalsCreated;
    private readonly Counter<long> _goalsUpdated;
    private readonly Counter<long> _pipelineFailures;
    private readonly Counter<long> _circuitOpen;
    private readonly Counter<long> _forecastPanelRequests;
    private readonly Histogram<double> _forecastPanelLatency;

    /// <summary>
    /// Inicializa os instrumentos de métrica usando a factory fornecida pelo DI.
    /// </summary>
    public GoalForecastMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(new MeterOptions(MeterName) { Version = "0.1.0" });

        _goalsCreated = _meter.CreateCounter<long>(
            GoalsCreatedTotalName,
            unit: "{metas}",
            description: "Total de metas criadas no módulo goal-forecast.");

        _goalsUpdated = _meter.CreateCounter<long>(
            GoalsUpdatedTotalName,
            unit: "{metas}",
            description: "Total de metas atualizadas no módulo goal-forecast.");

        _pipelineFailures = _meter.CreateCounter<long>(
            PipelineReaderFailuresTotalName,
            unit: "{falhas}",
            description: "Total de falhas na leitura do pipeline (timeout ou erro).");

        _circuitOpen = _meter.CreateCounter<long>(
            PipelineCircuitOpenTotalName,
            unit: "{aberturas}",
            description: "Total de aberturas do circuit breaker do pipeline reader.");

        _forecastPanelRequests = _meter.CreateCounter<long>(
            ForecastPanelRequestsTotalName,
            unit: "{requisicoes}",
            description: "Total de requisições ao painel comparativo GET /forecast.");

        _forecastPanelLatency = _meter.CreateHistogram<double>(
            ForecastPanelLatencyMsName,
            unit: "ms",
            description: "Latência do painel comparativo em milissegundos (SLO p95 ≤ 3.000 ms).");
    }

    // ── Métodos de emissão ────────────────────────────────────────────────────

    /// <summary>
    /// Registra criação de meta.
    /// Atributos: tenant_id, bu_id (não inclui valorMeta — RNF-7.3).
    /// </summary>
    public void RecordGoalCreated(string tenantId, string? buId)
    {
        _goalsCreated.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId),
            new KeyValuePair<string, object?>("bu_id", buId));
    }

    /// <summary>
    /// Registra atualização de meta.
    /// Atributos: tenant_id, bu_id (não inclui delta — RNF-7.3).
    /// </summary>
    public void RecordGoalUpdated(string tenantId, string? buId)
    {
        _goalsUpdated.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId),
            new KeyValuePair<string, object?>("bu_id", buId));
    }

    /// <summary>
    /// Registra falha na leitura do pipeline (timeout ou exceção).
    /// Atributos: tenant_id, bu_id para correlação de incidentes (RNF 7.2).
    /// </summary>
    public void RecordPipelineReaderFailure(string? tenantId, string? buId)
    {
        _pipelineFailures.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId),
            new KeyValuePair<string, object?>("bu_id", buId));
    }

    /// <summary>
    /// Registra abertura do circuit breaker do pipeline reader.
    /// Alerta: taxa elevada → alerta de degradação (design §11).
    /// </summary>
    public void RecordCircuitOpen()
    {
        _circuitOpen.Add(1);
    }

    /// <summary>
    /// Registra requisição ao painel comparativo com latência observada.
    /// Atributos: pipeline_unavailable (bool) para correlacionar com degradação.
    /// </summary>
    public void RecordForecastPanelRequest(double latencyMs, bool pipelineUnavailable)
    {
        var tags = new KeyValuePair<string, object?>("pipeline_unavailable", pipelineUnavailable);
        _forecastPanelRequests.Add(1, tags);
        _forecastPanelLatency.Record(latencyMs, tags);
    }

    /// <inheritdoc/>
    public void Dispose() => _meter.Dispose();
}
