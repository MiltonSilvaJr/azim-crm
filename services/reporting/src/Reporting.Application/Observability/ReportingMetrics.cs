using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Reporting.Application.Observability;

/// <summary>
/// Métricas do módulo reporting expostas via <c>System.Diagnostics.Metrics</c> (.NET nativo).
///
/// Métricas em snake_case (convenção Prometheus/OpenTelemetry — design §11, RNF 6.2):
/// <list type="bullet">
///   <item><description><c>reports_generated_total</c> — contador por tipo e outcome.</description></item>
///   <item><description><c>report_generation_duration_seconds</c> — histograma de latência por tipo.</description></item>
///   <item><description><c>report_export_duration_seconds</c> — histograma de duração de export.</description></item>
///   <item><description><c>report_rls_denied_total</c> — contador de tentativas bloqueadas por RLS (ADR-0001).</description></item>
///   <item><description><c>report_scope_denied_total</c> — contador de bloqueios de escopo RBAC (RNF 5).</description></item>
/// </list>
///
/// Singleton seguro para acesso concorrente. Registrado via DI.
///
/// Mapeia: TASK-24, design §11, RNF 6.2, ADR-0001.
/// </summary>
public sealed class ReportingMetrics : IDisposable
{
    /// <summary>Nome do medidor (instrument name) — escopo do módulo reporting.</summary>
    public const string MeterName = "reporting";

    private readonly Meter _meter;

    // ── Contadores ───────────────────────────────────────────────────────────

    /// <summary>
    /// Contador total de relatórios gerados com sucesso ou falha.
    /// Dimensões: <c>report_type</c>, <c>outcome</c> (success | error).
    /// </summary>
    private readonly Counter<long> _reportsGeneratedTotal;

    /// <summary>
    /// Contador de acessos bloqueados por RLS (falha-fechada ou violação detectada).
    /// Dimensões: <c>report_type</c>.
    /// </summary>
    private readonly Counter<long> _rlsDeniedTotal;

    /// <summary>
    /// Contador de bloqueios de escopo RBAC (PlatformOperator ou papel sem escopo).
    /// Dimensões: <c>report_type</c>, <c>role</c>.
    /// </summary>
    private readonly Counter<long> _scopeDeniedTotal;

    // ── Histogramas ──────────────────────────────────────────────────────────

    /// <summary>
    /// Histograma de duração de geração de relatório em segundos.
    /// Dimensões: <c>report_type</c>.
    /// Permite calcular p95 ≤ 3 s (RNF 1.1, PTV-01).
    /// </summary>
    private readonly Histogram<double> _generationDurationSeconds;

    /// <summary>
    /// Histograma de duração de export CSV em segundos.
    /// Dimensões: <c>report_type</c>.
    /// Permite calcular p95 ≤ 10 s (RNF 3.1).
    /// </summary>
    private readonly Histogram<double> _exportDurationSeconds;

    /// <summary>Inicializa as métricas do módulo reporting.</summary>
    public ReportingMetrics()
    {
        _meter = new Meter(MeterName, "0.1.0");

        _reportsGeneratedTotal = _meter.CreateCounter<long>(
            name: "reports_generated_total",
            unit: "{reports}",
            description: "Número total de relatórios gerados. Dimensões: report_type, outcome.");

        _rlsDeniedTotal = _meter.CreateCounter<long>(
            name: "report_rls_denied_total",
            unit: "{denials}",
            description: "Número de acessos bloqueados por RLS (falha-fechada ou violação). Dimensões: report_type.");

        _scopeDeniedTotal = _meter.CreateCounter<long>(
            name: "report_scope_denied_total",
            unit: "{denials}",
            description: "Número de bloqueios de escopo RBAC (PlatformOperator ou papel sem permissão). Dimensões: report_type, role.");

        _generationDurationSeconds = _meter.CreateHistogram<double>(
            name: "report_generation_duration_seconds",
            unit: "s",
            description: "Duração de geração de relatório em segundos. Monitorar p95 ≤ 3 s (RNF 1.1, PTV-01). Dimensões: report_type.");

        _exportDurationSeconds = _meter.CreateHistogram<double>(
            name: "report_export_duration_seconds",
            unit: "s",
            description: "Duração de geração de export CSV em segundos. Monitorar p95 ≤ 10 s (RNF 3.1). Dimensões: report_type.");
    }

    // ── Métodos de emissão ───────────────────────────────────────────────────

    /// <summary>
    /// Registra a geração de um relatório (sucesso ou erro).
    /// </summary>
    /// <param name="reportType">Tipo do relatório em snake_case (ex.: "funnel", "forecast").</param>
    /// <param name="outcome">Resultado: "success" ou "error".</param>
    /// <param name="durationSeconds">Duração em segundos.</param>
    public void RecordReportGenerated(string reportType, string outcome, double durationSeconds)
    {
        var tags = new TagList
        {
            { "report_type", reportType },
            { "outcome",     outcome    }
        };
        _reportsGeneratedTotal.Add(1, tags);
        _generationDurationSeconds.Record(durationSeconds, new TagList { { "report_type", reportType } });
    }

    /// <summary>
    /// Registra a geração de um export CSV.
    /// </summary>
    /// <param name="reportType">Tipo do relatório em snake_case.</param>
    /// <param name="durationSeconds">Duração em segundos.</param>
    public void RecordExportGenerated(string reportType, double durationSeconds)
    {
        _exportDurationSeconds.Record(durationSeconds, new TagList { { "report_type", reportType } });
    }

    /// <summary>
    /// Registra uma tentativa bloqueada por RLS (falha-fechada ou violação detectada).
    /// </summary>
    /// <param name="reportType">Tipo do relatório em snake_case.</param>
    public void RecordRlsDenied(string reportType)
    {
        _rlsDeniedTotal.Add(1, new TagList { { "report_type", reportType } });
    }

    /// <summary>
    /// Registra um bloqueio de escopo RBAC (PlatformOperator ou papel sem permissão).
    /// </summary>
    /// <param name="reportType">Tipo do relatório em snake_case.</param>
    /// <param name="role">Papel que tentou o acesso.</param>
    public void RecordScopeDenied(string reportType, string role)
    {
        _scopeDeniedTotal.Add(1, new TagList
        {
            { "report_type", reportType },
            { "role",        role       }
        });
    }

    /// <inheritdoc/>
    public void Dispose() => _meter.Dispose();
}
