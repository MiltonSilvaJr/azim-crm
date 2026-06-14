namespace ActivityManagement.Application.Ports;

/// <summary>
/// Port de leitura para validação de vínculos com oportunidades (Req 3.2).
/// Implementado em Infrastructure via HTTP/gRPC interno (mTLS).
/// Handlers de Application usam esta interface; nunca referenciam o adapter concreto.
/// Mapeia: design §6.4, Req 3, TASK-07.
/// </summary>
public interface IOpportunityReadPort
{
    /// <summary>
    /// Verifica se a oportunidade existe e pertence ao tenant informado.
    /// Retorna <c>false</c> para IDs inexistentes ou de outro tenant (anti-enumeração).
    /// </summary>
    /// <param name="opportunityId">Identificador da oportunidade.</param>
    /// <param name="tenantId">Tenant autenticado na operação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<bool> ExistsAsync(Guid opportunityId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se a oportunidade está aberta (não fechada/perdida) no tenant informado.
    /// Usado por <c>SuggestNextActivityQuery</c> para decidir se a sugestão é aplicável.
    /// Retorna <c>false</c> quando inexistente, de outro tenant ou fechada.
    /// </summary>
    /// <param name="opportunityId">Identificador da oportunidade.</param>
    /// <param name="tenantId">Tenant autenticado na operação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<bool> IsOpenAsync(Guid opportunityId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna os IDs das oportunidades abertas na BU informada que possuem pelo menos
    /// uma atividade não terminal vinculada.
    /// Usado por <c>GetOpportunitiesWithoutFollowupQuery</c>.
    /// </summary>
    /// <param name="buId">Identificador da Business Unit.</param>
    /// <param name="tenantId">Tenant autenticado na operação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<IReadOnlyList<Guid>> GetOpenOpportunityIdsForBuAsync(
        Guid buId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
