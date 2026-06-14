using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Services;

/// <summary>
/// Especificação de domínio pura: detecta oportunidades estagnadas.
/// Critério: stage_category = open E (now − last_activity_at) ≥ 14 dias corridos.
/// Sem dependências de infraestrutura. IClock injetado via parâmetro.
/// Mapeia: Req 17, RN-028, DD-005, design §4.6.
/// </summary>
public static class StagnationSpecification
{
    /// <summary>Quantidade de dias corridos para considerar estagnação.</summary>
    public const int StaleDaysThreshold = 14;

    /// <summary>
    /// Verifica se a oportunidade está estagnada.
    /// </summary>
    /// <param name="stageCategory">Categoria atual da oportunidade.</param>
    /// <param name="lastActivityAt">Data/hora da última atividade.</param>
    /// <param name="now">Instante atual (injetado para testabilidade).</param>
    /// <returns>True se a oportunidade estiver open e sem atividade há ≥ 14 dias.</returns>
    public static bool IsSatisfiedBy(
        StageCategory stageCategory,
        DateTimeOffset lastActivityAt,
        DateTimeOffset now)
    {
        if (stageCategory != StageCategory.Open)
            return false;

        var daysSinceLastActivity = (now - lastActivityAt).TotalDays;
        return daysSinceLastActivity >= StaleDaysThreshold;
    }
}
