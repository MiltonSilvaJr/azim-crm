namespace GoalForecast.Application.Ports;

/// <summary>
/// Porta de saída para verificação de membership owner↔BU no contexto organization.
/// Implementada na camada Infrastructure (BuMembershipReader).
///
/// Utilizada pelo <c>CreateOrUpdateGoalCommandHandler</c> para validar Req 1.4
/// antes de criar/atualizar meta no escopo RESPONSAVEL (DD-004).
///
/// Mapeia: Req 1.4, DD-004, design §5.1, §6.4, TASK-08.
/// </summary>
public interface IBuMembershipReader
{
    /// <summary>
    /// Verifica se <paramref name="ownerId"/> é membro ativo da BU <paramref name="buId"/>
    /// dentro do tenant <paramref name="tenantId"/>.
    /// </summary>
    /// <param name="tenantId">Tenant do principal autenticado.</param>
    /// <param name="ownerId">Identificador do responsável a verificar.</param>
    /// <param name="buId">Identificador da unidade de negócio.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns><c>true</c> quando o owner é membro ativo da BU; <c>false</c> caso contrário.</returns>
    Task<bool> IsOwnerMemberOfBu(
        Guid tenantId,
        Guid ownerId,
        Guid buId,
        CancellationToken cancellationToken = default);
}
