namespace Authentication.Domain.Specifications;

/// <summary>
/// Specification que verifica se o <c>identity_uid</c> validado possui
/// um <c>user_id</c> ativo no tenant corrente.
///
/// Regra (design.md § 4.6, Req 5.4):
///   <c>identity_uid</c> validado possui <c>user_id</c> ativo no tenant (organization).
///   Falha → 403 AUTH-ERR-005.
///
/// Nota: esta specification opera sobre os dados já resolvidos pelo <c>IUserDirectory</c>
/// (módulo organization). Os parâmetros recebidos são o resultado dessa resolução.
/// O <c>identity_uid</c> nunca é exposto nesta camada (DD-001).
///
/// Pure function — sem efeito colateral.
///
/// Mapeia: Req 5.4, design.md § 4.6.
/// </summary>
public static class ActiveUserSpec
{
    /// <summary>
    /// Avalia se o usuário está ativo no tenant.
    /// </summary>
    /// <param name="userId">UUID do usuário no módulo organization (<see cref="Guid.Empty"/> quando não encontrado).</param>
    /// <param name="isActive">Indica se o usuário está ativo no tenant.</param>
    /// <returns>
    /// <see langword="true"/> quando <paramref name="userId"/> não é <see cref="Guid.Empty"/>
    /// e <paramref name="isActive"/> é <see langword="true"/>.
    /// </returns>
    public static bool IsSatisfiedBy(Guid userId, bool isActive) =>
        userId != Guid.Empty && isActive;
}
