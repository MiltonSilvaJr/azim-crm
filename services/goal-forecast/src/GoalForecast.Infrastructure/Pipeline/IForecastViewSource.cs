using GoalForecast.Application.Ports;

namespace GoalForecast.Infrastructure.Pipeline;

/// <summary>
/// Porta interna que abstrai a fonte de dados do read model ForecastView.
/// Separada de <see cref="IPipelineForecastReader"/> para permitir substituição
/// nos testes (stub sem banco) enquanto o reader controla a resiliência.
///
/// Implementação de produção: lê in-process do opportunity-pipeline (DD-005).
/// Implementação de teste: <c>StubForecastViewSource</c>.
///
/// Mapeia: DD-005, design §6.4, TASK-19.
/// </summary>
public interface IForecastViewSource
{
    /// <summary>
    /// Lê realizado e pipeline ponderado do read model ForecastView.
    /// Pode lançar qualquer exceção — o caller (PipelineForecastReader) trata via Polly.
    /// </summary>
    Task<(long WonTotalCents, long ForecastPonderadoCents)> ReadAsync(
        ForecastViewQuery query,
        CancellationToken cancellationToken = default);
}
