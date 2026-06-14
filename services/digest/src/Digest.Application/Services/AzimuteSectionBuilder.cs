using Digest.Application.Models;
using Digest.Application.Ports;
using Digest.Domain.ValueObjects;

namespace Digest.Application.Services;

/// <summary>
/// Serviço de Application que compõe o bloco de azimute semanal para usuários de gestão na segunda-feira.
/// Monta <c>azimute_pipeline</c> (pipeline ponderado, variação, ganhos/fechamentos) e <c>azimute_metas</c>.
/// Omite completamente o bloco de metas quando <see cref="IForecastReadPort"/> retorna <see langword="null"/>
/// — sem erro, sem placeholder (Req 5.4, RN-018, PBT-04, DD-010).
/// Todos os valores monetários via <see cref="MoneyCents"/> (DD-010).
/// </summary>
public sealed class AzimuteSectionBuilder
{
    /// <summary>Chave do bloco de pipeline do azimute.</summary>
    public const string AzimutePipelineKey = "azimute_pipeline";

    /// <summary>Chave do bloco de metas do azimute.</summary>
    public const string AzuimuteMetasKey = "azimute_metas";

    private readonly IOpportunityReadPort _opportunityPort;
    private readonly IForecastReadPort _forecastPort;

    /// <summary>
    /// Constrói o builder com as portas de leitura necessárias.
    /// </summary>
    public AzimuteSectionBuilder(
        IOpportunityReadPort opportunityPort,
        IForecastReadPort forecastPort)
    {
        _opportunityPort = opportunityPort;
        _forecastPort = forecastPort;
    }

    /// <summary>
    /// Compõe o azimute para o tenant e data especificados.
    /// Retorna os blocos compostos; nunca lança exceção por ausência de meta (PBT-04).
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="referenceDate">Data de referência local (deve ser segunda-feira).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de <see cref="DigestSection"/>s do azimute. Pode conter apenas o pipeline (sem metas).</returns>
    public async Task<IReadOnlyList<DigestSection>> BuildAsync(
        Guid tenantId,
        DateOnly referenceDate,
        CancellationToken cancellationToken = default)
    {
        var sections = new List<DigestSection>();

        // --- Bloco azimute_pipeline ---
        var pipeline = await _opportunityPort.GetWeightedPipelineAsync(tenantId, cancellationToken);
        var pipelineItems = pipeline
            .Select(o => FormatPipelineItem(o))
            .ToList();

        sections.Add(new DigestSection(AzimutePipelineKey, pipelineItems));

        // --- Bloco azimute_metas (omitido completamente se null — PBT-04, Req 5.4, RN-018) ---
        var forecast = await _forecastPort.GetForecastBlockAsync(tenantId, referenceDate, cancellationToken);
        if (forecast is not null)
        {
            var metasItems = BuildMetasItems(forecast);
            sections.Add(new DigestSection(AzuimuteMetasKey, metasItems));
        }
        // forecast == null → bloco de metas omitido sem erro e sem placeholder

        return sections.AsReadOnly();
    }

    // ------------------------------------------------------------------
    // Helpers privados — formatação de itens (sem float/double — DD-010)
    // ------------------------------------------------------------------

    private static string FormatPipelineItem(OpportunityItem item) =>
        $"{item.Name}: {item.WeightedValue.Cents / 100m:F2}";

    private static List<string> BuildMetasItems(ForecastBlock forecast)
    {
        return new List<string>
        {
            $"pipeline_ponderado: {forecast.PipelineWeighted.Cents / 100m:F2}",
            $"variacao: {forecast.PipelineVariation.Cents / 100m:F2}",
            $"realizado: {forecast.RevenueRealized.Cents / 100m:F2}",
            $"meta: {forecast.RevenueGoal.Cents / 100m:F2}",
            $"ganhas: {forecast.WonCount}",
            $"perdidas: {forecast.LostCount}",
        };
    }
}
