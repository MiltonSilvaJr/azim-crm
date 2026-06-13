namespace Organization.Application.Ports;

/// <summary>
/// Port de saída para contar TAdmins ativos no tenant.
/// Usado pela <c>LastTenantAdminPolicy</c> com leitura serializável/lock dentro da transação (DD-003).
/// </summary>
public interface ITenantAdminCounter
{
    /// <summary>
    /// Retorna o número de usuários com papel <c>TAdmin</c> e status <c>active=true</c> no tenant corrente.
    /// Executado dentro de uma transação para evitar corrida (DD-003).
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<int> CountActiveTenantAdminsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
