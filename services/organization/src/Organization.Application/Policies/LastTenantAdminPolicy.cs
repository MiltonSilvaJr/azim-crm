using Organization.Application.Ports;

namespace Organization.Application.Policies;

/// <summary>
/// Domain policy de aplicação: impede que o tenant fique sem ao menos um TAdmin ativo.
/// Executada dentro da transação do handler com lock serializável via <see cref="ITenantAdminCounter"/> (DD-003).
///
/// Usada por:
/// - <c>ChangeMembershipRoleCommandHandler</c> (rebaixamento de TAdmin)
/// - <c>RemoveMembershipCommandHandler</c> (remoção de membership de TAdmin)
/// - <c>DeactivateUserCommandHandler</c> (desativação de TAdmin)
///
/// PBT-02: nenhuma sequência de operações deve deixar o tenant sem TAdmin ativo.
/// </summary>
public static class LastTenantAdminPolicy
{
    /// <summary>
    /// Verifica se a operação deixaria o tenant sem TAdmin ativo.
    /// Lança <see cref="InvalidOperationException"/> com ORG-ERR-009 se a invariante seria violada.
    /// </summary>
    /// <param name="adminCounter">Contador de TAdmins ativos.</param>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="affectedUserId">Identificador do usuário afetado pela operação.</param>
    /// <param name="affectedUserIsTAdmin">Indica se o usuário afetado é atualmente TAdmin.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <exception cref="InvalidOperationException">ORG-ERR-009 quando violaria a invariante.</exception>
    public static async Task EnforceAsync(
        ITenantAdminCounter adminCounter,
        Guid tenantId,
        Guid affectedUserId,
        bool affectedUserIsTAdmin,
        CancellationToken cancellationToken = default)
    {
        // Somente TAdmins são relevantes para a política
        if (!affectedUserIsTAdmin)
            return;

        var currentCount = await adminCounter.CountActiveTenantAdminsAsync(tenantId, cancellationToken);

        // Se há mais de 1 TAdmin ativo, a operação é permitida
        if (currentCount > 1)
            return;

        throw new InvalidOperationException(
            "O tenant precisa ter ao menos um administrador ativo. Promova outro usuário antes de remover este. ORG-ERR-009");
    }
}
