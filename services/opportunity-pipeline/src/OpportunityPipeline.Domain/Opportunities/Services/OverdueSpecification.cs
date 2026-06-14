using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Services;

/// <summary>
/// Especificação de domínio pura: detecta oportunidades vencidas.
/// Critério: stage_category = open E expected_close_date &lt; hoje.
/// Derivada em leitura, não persistida.
/// Mapeia: Req 9.3, design §4.6.
/// </summary>
public static class OverdueSpecification
{
    /// <summary>
    /// Verifica se a oportunidade está vencida (data de fechamento no passado).
    /// </summary>
    /// <param name="stageCategory">Categoria atual da oportunidade.</param>
    /// <param name="expectedCloseDate">Data esperada de fechamento (pode ser nula).</param>
    /// <param name="today">Data atual (injetada para testabilidade).</param>
    /// <returns>True se a oportunidade for open com data de fechamento no passado.</returns>
    public static bool IsSatisfiedBy(
        StageCategory stageCategory,
        DateOnly? expectedCloseDate,
        DateOnly today)
    {
        if (stageCategory != StageCategory.Open)
            return false;

        if (expectedCloseDate is null)
            return false;

        return expectedCloseDate.Value < today;
    }
}
