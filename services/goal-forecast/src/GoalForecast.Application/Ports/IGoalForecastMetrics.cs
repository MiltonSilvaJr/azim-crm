namespace GoalForecast.Application.Ports;

/// <summary>
/// Porta de saída para emissão de métricas de negócio do módulo goal-forecast.
///
/// A Application define o contrato; a Infrastructure fornece a implementação
/// concreta com os instrumentos de OpenTelemetry/Prometheus (Clean Architecture, DD-001).
///
/// Nenhum método expõe valorMeta, dados pessoais ou segredos (RNF-7.3).
///
/// Mapeia: TASK-28, RNF 7.2, design §11.
/// </summary>
public interface IGoalForecastMetrics
{
    /// <summary>
    /// Registra criação de meta.
    /// Não inclui valorMeta nos atributos — RNF-7.3.
    /// </summary>
    void RecordGoalCreated(string tenantId, string? buId);

    /// <summary>
    /// Registra atualização de meta.
    /// Não inclui delta de valorMeta — RNF-7.3.
    /// </summary>
    void RecordGoalUpdated(string tenantId, string? buId);

    /// <summary>
    /// Registra falha na leitura do pipeline (timeout ou exceção).
    /// </summary>
    void RecordPipelineReaderFailure(string? tenantId, string? buId);

    /// <summary>
    /// Registra abertura do circuit breaker do pipeline reader.
    /// Alerta: taxa elevada → alerta de degradação (design §11).
    /// </summary>
    void RecordCircuitOpen();

    /// <summary>
    /// Registra requisição ao painel comparativo com latência observada em milissegundos.
    /// </summary>
    void RecordForecastPanelRequest(double latencyMs, bool pipelineUnavailable);
}
