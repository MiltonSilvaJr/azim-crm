using System.Diagnostics;
using System.Diagnostics.Metrics;
using DataMigration.Application.Ports;

namespace DataMigration.Infrastructure.Observability;

/// <summary>
/// Implementação de métricas do módulo data-migration usando
/// <see cref="System.Diagnostics.Metrics"/> (OpenTelemetry-compatível).
///
/// Métricas expostas (RNF 6.1, design §11):
/// - <c>migration_rows_processed_total</c>: linhas processadas com sucesso.
/// - <c>migration_rows_failed_total</c>: linhas com falha.
/// - <c>migration_duration_seconds</c>: histograma de duração do import.
/// - <c>migration_forecast_divergences_total</c>: divergências de forecast detectadas.
/// - <c>migration_jobs_state_total</c>: transições de estado por label.
///
/// O nome do Meter (<c>DataMigration</c>) deve ser registrado no
/// OpenTelemetry SDK do host (Program.cs) para coleta automatizada.
///
/// Rastreia: design §11, RNF 6.1, TASK-25.
/// </summary>
public sealed class MigrationMetrics : IMigrationMetrics, IDisposable
{
    /// <summary>Nome do meter para registro no OpenTelemetry SDK.</summary>
    public const string MeterName = "DataMigration";

    private readonly Meter _meter;
    private readonly Counter<long> _rowsProcessed;
    private readonly Counter<long> _rowsFailed;
    private readonly Histogram<double> _durationSeconds;
    private readonly Counter<long> _forecastDivergences;
    private readonly Counter<long> _jobsStateTotal;

    /// <summary>
    /// ActivitySource para traces distribuídos (OpenTelemetry).
    /// Spans por etapa: parse, dry-run, import.
    /// </summary>
    public static readonly ActivitySource ActivitySource =
        new(MeterName, version: "0.1.0");

    /// <summary>Cria o conjunto de instrumentos de métricas.</summary>
    public MigrationMetrics()
    {
        _meter = new Meter(MeterName, version: "0.1.0");

        _rowsProcessed = _meter.CreateCounter<long>(
            name: "migration_rows_processed_total",
            unit: "{rows}",
            description: "Total de linhas processadas com sucesso no import de migração.");

        _rowsFailed = _meter.CreateCounter<long>(
            name: "migration_rows_failed_total",
            unit: "{rows}",
            description: "Total de linhas com falha no import de migração.");

        _durationSeconds = _meter.CreateHistogram<double>(
            name: "migration_duration_seconds",
            unit: "s",
            description: "Duração do import de migração medida de started_at a finished_at (RNF 1.2).");

        _forecastDivergences = _meter.CreateCounter<long>(
            name: "migration_forecast_divergences_total",
            unit: "{divergences}",
            description: "Total de divergências de forecast detectadas no dry-run.");

        _jobsStateTotal = _meter.CreateCounter<long>(
            name: "migration_jobs_state_total",
            unit: "{jobs}",
            description: "Transições de estado de jobs de migração por estado destino.");
    }

    /// <inheritdoc />
    public void IncrementRowsProcessed() =>
        _rowsProcessed.Add(1);

    /// <inheritdoc />
    public void IncrementRowsFailed() =>
        _rowsFailed.Add(1);

    /// <inheritdoc />
    public void RecordDuration(TimeSpan duration) =>
        _durationSeconds.Record(duration.TotalSeconds);

    /// <inheritdoc />
    public void IncrementForecastDivergences() =>
        _forecastDivergences.Add(1);

    /// <inheritdoc />
    public void IncrementJobState(string state) =>
        _jobsStateTotal.Add(1, new KeyValuePair<string, object?>("state", state));

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();

    // =========================================================================
    // Helpers para criação de spans de trace (design §11)
    // =========================================================================

    /// <summary>
    /// Inicia um span de trace para a etapa de parsing.
    /// </summary>
    public static Activity? StartParseActivity(Guid jobId, Guid correlationId)
    {
        var activity = ActivitySource.StartActivity("migration.parse");
        activity?.SetTag("migration.job_id", jobId.ToString());
        activity?.SetTag("correlation_id", correlationId.ToString());
        return activity;
    }

    /// <summary>
    /// Inicia um span de trace para a etapa de dry-run.
    /// </summary>
    public static Activity? StartDryRunActivity(Guid jobId, Guid correlationId)
    {
        var activity = ActivitySource.StartActivity("migration.dry_run");
        activity?.SetTag("migration.job_id", jobId.ToString());
        activity?.SetTag("correlation_id", correlationId.ToString());
        return activity;
    }

    /// <summary>
    /// Inicia um span de trace para a etapa de import.
    /// </summary>
    public static Activity? StartImportActivity(Guid jobId, Guid correlationId)
    {
        var activity = ActivitySource.StartActivity("migration.import");
        activity?.SetTag("migration.job_id", jobId.ToString());
        activity?.SetTag("correlation_id", correlationId.ToString());
        return activity;
    }
}
