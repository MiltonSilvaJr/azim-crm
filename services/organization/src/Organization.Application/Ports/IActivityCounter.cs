namespace Organization.Application.Ports;

/// <summary>
/// Port para contar atividades futuras de um usuário dentro de um tenant.
/// Usado pela <c>FutureActivitiesSpec</c> para bloquear desativação de usuário
/// com compromissos pendentes (ORG-ERR-011).
/// </summary>
public interface IActivityCounter
{
    /// <summary>
    /// Retorna a quantidade de atividades futuras vinculadas ao usuário no tenant.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="userId">Identificador do usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Número de atividades futuras (≥0).</returns>
    Task<int> CountFutureAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}
