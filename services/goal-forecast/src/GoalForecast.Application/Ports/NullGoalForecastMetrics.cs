namespace GoalForecast.Application.Ports;

/// <summary>
/// Implementação nula de <see cref="IGoalForecastMetrics"/> para uso em testes
/// ou contextos onde observabilidade não está configurada.
/// Segue o padrão Null Object: todos os métodos são no-op.
/// </summary>
public sealed class NullGoalForecastMetrics : IGoalForecastMetrics
{
    /// <summary>Instância singleton compartilhável.</summary>
    public static readonly NullGoalForecastMetrics Instance = new();

    /// <inheritdoc/>
    public void RecordGoalCreated(string tenantId, string? buId) { }

    /// <inheritdoc/>
    public void RecordGoalUpdated(string tenantId, string? buId) { }

    /// <inheritdoc/>
    public void RecordPipelineReaderFailure(string? tenantId, string? buId) { }

    /// <inheritdoc/>
    public void RecordCircuitOpen() { }

    /// <inheritdoc/>
    public void RecordForecastPanelRequest(double latencyMs, bool pipelineUnavailable) { }
}
