namespace DataMigration.Application.Ports;

/// <summary>
/// Porta de métricas do módulo data-migration (design §11, RNF 6).
///
/// Expõe os contadores e histogramas obrigatórios:
/// - <c>migration_rows_processed_total</c>
/// - <c>migration_rows_failed_total</c>
/// - <c>migration_duration_seconds</c> (histograma)
/// - <c>migration_forecast_divergences_total</c>
/// - <c>migration_jobs_state_total{state}</c>
///
/// Implementação em Infrastructure (OpenTelemetry ou System.Diagnostics.Metrics).
/// Permite substituição por stub em testes.
///
/// Rastreia: design §11, RNF 6.1, TASK-25.
/// </summary>
public interface IMigrationMetrics
{
    /// <summary>
    /// Incrementa o contador de linhas processadas com sucesso.
    /// snake_case conforme convenção do projeto (design §11).
    /// </summary>
    void IncrementRowsProcessed();

    /// <summary>
    /// Incrementa o contador de linhas com falha.
    /// </summary>
    void IncrementRowsFailed();

    /// <summary>
    /// Registra a duração de um job no histograma <c>migration_duration_seconds</c>.
    /// </summary>
    /// <param name="duration">Duração medida de <c>started_at</c> a <c>finished_at</c>.</param>
    void RecordDuration(TimeSpan duration);

    /// <summary>
    /// Incrementa o contador de divergências de forecast detectadas.
    /// </summary>
    void IncrementForecastDivergences();

    /// <summary>
    /// Registra a transição de estado de um job.
    /// </summary>
    /// <param name="state">
    /// Estado destino (snake_case): <c>created</c>, <c>dry_run_completed</c>,
    /// <c>triage_in_progress</c>, <c>ready_to_import</c>, <c>importing</c>,
    /// <c>completed</c>, <c>rolled_back</c>, <c>failed</c>.
    /// </param>
    void IncrementJobState(string state);
}
