namespace GoalForecast.Application.Ports;

/// <summary>
/// Porta de saída para leitura do read model ForecastView do opportunity-pipeline.
/// Implementada na camada Infrastructure (PipelineForecastReader) com circuit breaker.
///
/// Contrato de resiliência (RNF 6, DD-007):
/// <list type="bullet">
///   <item>Em falha ou timeout, retorna <see cref="ForecastViewResult.Unavailable"/> — nunca lança.</item>
///   <item>Circuit breaker aberto retorna indisponível imediatamente.</item>
///   <item>Nunca escreve no pipeline (Req 8.3).</item>
/// </list>
///
/// Mapeia: Req 5, Req 8, RNF 6, DD-005, DD-007, RISK-GOAL-01, design §6.4, TASK-08.
/// </summary>
public interface IPipelineForecastReader
{
    /// <summary>
    /// Lê realizado (<c>won_total</c>) e pipeline ponderado (<c>forecast_ponderado</c>)
    /// do read model ForecastView para o escopo e período informados.
    /// </summary>
    /// <param name="query">Parâmetros de consulta: tenant, BU, owner, período.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// Resultado com valores em centavos e flag <c>Available</c>.
    /// Quando indisponível, retorna <see cref="ForecastViewResult.Unavailable"/>.
    /// </returns>
    Task<ForecastViewResult> Read(
        ForecastViewQuery query,
        CancellationToken cancellationToken = default);
}
