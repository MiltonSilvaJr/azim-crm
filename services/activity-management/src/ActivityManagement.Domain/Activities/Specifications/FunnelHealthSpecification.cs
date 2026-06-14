namespace ActivityManagement.Domain.Activities.Specifications;

/// <summary>
/// Specification que determina se uma oportunidade aberta possui follow-up futuro.
/// Regra: oportunidade tem follow-up ⟺ existe ≥1 atividade não terminal
/// com <c>dueAt</c> estritamente no futuro vinculada a ela.
/// Não bloqueia operações — é sinalização (DD-006, Req 10.4).
/// Mapeia: design §4.6, Req 10.1, PBT-05, TASK-05.
/// </summary>
public sealed class FunnelHealthSpecification
{
    private readonly DateTimeOffset _referenceInstant;

    /// <summary>
    /// Inicializa a specification com o instante de referência.
    /// </summary>
    /// <param name="referenceInstant">Instante corrente (UTC).</param>
    public FunnelHealthSpecification(DateTimeOffset referenceInstant)
    {
        _referenceInstant = referenceInstant;
    }

    /// <summary>
    /// Verifica se a oportunidade tem pelo menos uma atividade não terminal com <c>dueAt</c> futuro.
    /// </summary>
    /// <param name="opportunityId">Identificador da oportunidade a verificar.</param>
    /// <param name="activities">Coleção de atividades vinculadas à oportunidade (pode conter atividades de outras oportunidades; o filtro é aplicado internamente).</param>
    /// <returns><c>true</c> quando existe ≥1 follow-up futuro válido.</returns>
    public bool HasFollowup(Guid opportunityId, IEnumerable<Activity> activities)
    {
        return activities.Any(a =>
            !a.Status.IsTerminal &&
            a.OpportunityLink?.OpportunityId == opportunityId &&
            a.DueAt.Value > _referenceInstant);
    }
}
