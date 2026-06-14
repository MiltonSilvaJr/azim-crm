namespace Organization.Application.Ports;

/// <summary>
/// Port de saída para consultar o número de oportunidades ativas em uma Business Unit.
/// Implementado in-process pelo módulo <c>opportunity-pipeline</c> (monólito modular, §6.4).
/// Usado pela <c>ActiveOpportunitiesSpec</c> para bloquear inativação de BU.
/// </summary>
public interface IOpportunityCounter
{
    /// <summary>
    /// Retorna o número de oportunidades ativas para a BU informada no tenant corrente.
    /// Falha fecha a operação (fail-closed): se o counter estiver indisponível, a inativação é bloqueada.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="buId">Identificador da Business Unit.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<int> CountActiveAsync(Guid tenantId, Guid buId, CancellationToken cancellationToken = default);
}
