namespace ActivityManagement.Application.Ports;

/// <summary>
/// Port de leitura para validação de vínculos com contas (Req 3.3).
/// Implementado em Infrastructure via HTTP/gRPC interno (mTLS).
/// Handlers de Application usam esta interface; nunca referenciam o adapter concreto.
/// Mapeia: design §6.4, Req 3, TASK-07.
/// </summary>
public interface IAccountReadPort
{
    /// <summary>
    /// Verifica se a conta existe e pertence ao tenant informado.
    /// Retorna <c>false</c> para IDs inexistentes ou de outro tenant (anti-enumeração).
    /// </summary>
    /// <param name="accountId">Identificador da conta.</param>
    /// <param name="tenantId">Tenant autenticado na operação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<bool> ExistsAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken = default);
}
