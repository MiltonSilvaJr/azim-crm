namespace GoalForecast.Application.Ports;

/// <summary>
/// Parâmetros de consulta ao read model ForecastView do opportunity-pipeline.
/// Mapeia: Req 8, DD-005, design §6.4, TASK-08.
/// </summary>
public sealed record ForecastViewQuery(
    Guid TenantId,
    Guid BuId,
    Guid? OwnerId,
    int Year,
    int Month);

/// <summary>
/// Resultado da leitura do read model ForecastView.
/// Quando <see cref="Available"/> é false (DD-007), os valores monetários são
/// indefinidos e devem ser ignorados — nunca exibir zero confundível.
/// Mapeia: Req 5, Req 8, RNF 6, DD-007, design §6.4, TASK-08.
/// </summary>
public sealed record ForecastViewResult(
    long WonTotalCents,
    long ForecastPonderadoCents,
    bool Available)
{
    /// <summary>
    /// Instância padrão representando pipeline indisponível (DD-007).
    /// Valores monetários são zero; Available=false sinaliza que devem ser ignorados.
    /// </summary>
    public static readonly ForecastViewResult Unavailable =
        new(WonTotalCents: 0L, ForecastPonderadoCents: 0L, Available: false);
}
