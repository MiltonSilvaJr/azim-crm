using GoalForecast.Application.Ports;

namespace GoalForecast.Infrastructure.Pipeline;

/// <summary>
/// Stub da fonte ForecastView para desenvolvimento local e testes sem o pipeline real.
/// Retorna zeros — nunca usado em produção.
///
/// Em produção, substituir por implementação que lê o read model real do opportunity-pipeline.
/// Mapeia: DD-005, design §6.4.
/// </summary>
public sealed class StubForecastViewSource : IForecastViewSource
{
    /// <inheritdoc/>
    public Task<(long WonTotalCents, long ForecastPonderadoCents)> ReadAsync(
        ForecastViewQuery query,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult((0L, 0L));
    }
}
