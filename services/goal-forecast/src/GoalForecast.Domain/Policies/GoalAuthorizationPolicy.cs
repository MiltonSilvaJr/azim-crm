using GoalForecast.Domain.Authorization;
using GoalForecast.Domain.ValueObjects;

namespace GoalForecast.Domain.Policies;

/// <summary>
/// Política de autorização pura do domínio que decide se um <see cref="GoalPrincipal"/>
/// pode escrever ou ler uma meta de determinado escopo.
///
/// Matriz RBAC (design §10, Req 12):
/// <list type="table">
///   <listheader><term>Papel</term><term>Escrever</term><term>Ler</term></listheader>
///   <item><term>TenantAdmin</term><description>Qualquer BU</description><description>Tenant inteiro</description></item>
///   <item><term>GestorDeBu</term><description>Só sua BU</description><description>Sua BU</description></item>
///   <item><term>Executivo</term><description>Negado</description><description>Tenant inteiro</description></item>
///   <item><term>Vendedor</term><description>Negado</description><description>Só owner_id próprio</description></item>
/// </list>
///
/// Pura: sem I/O, sem injeção de dependência, determinística.
/// Negação retorna GF-ERR-006 sem revelar existência do recurso (RNF-2.3).
/// TenantId divergente entre principal e tenant alvo resulta em negação (INV-4).
///
/// Mapeia: Req 12, RNF 2, RNF-2.3, design §4.6, §10, TASK-07.
/// </summary>
public static class GoalAuthorizationPolicy
{
    /// <summary>
    /// Verifica se o <paramref name="principal"/> pode escrever (criar/editar)
    /// uma meta no <paramref name="scope"/> do <paramref name="tenantId"/> alvo.
    /// </summary>
    /// <param name="principal">Principal autenticado.</param>
    /// <param name="scope">Escopo da meta a ser criada/editada.</param>
    /// <param name="tenantId">Tenant ao qual a meta pertence.</param>
    /// <returns>Resultado de autorização.</returns>
    public static AuthorizationResult CanWrite(GoalPrincipal principal, GoalScope scope, Guid tenantId)
    {
        // INV-4: tenant_id divergente é sempre negado.
        if (principal.TenantId != tenantId)
            return AuthorizationResult.Denied();

        return principal.Role switch
        {
            GoalRole.TenantAdmin =>
                // TenantAdmin pode criar/editar em qualquer BU do tenant.
                AuthorizationResult.Allowed,

            GoalRole.GestorDeBu =>
                // GestorDeBu pode criar/editar somente na sua BU.
                principal.BuId == scope.BuId
                    ? AuthorizationResult.Allowed
                    : AuthorizationResult.Denied(),

            // Executivo e Vendedor não podem escrever.
            _ => AuthorizationResult.Denied()
        };
    }

    /// <summary>
    /// Verifica se o <paramref name="principal"/> pode ler
    /// uma meta no <paramref name="scope"/> do <paramref name="tenantId"/> alvo.
    /// </summary>
    /// <param name="principal">Principal autenticado.</param>
    /// <param name="scope">Escopo da meta a ser lida.</param>
    /// <param name="tenantId">Tenant ao qual a meta pertence.</param>
    /// <returns>Resultado de autorização.</returns>
    public static AuthorizationResult CanRead(GoalPrincipal principal, GoalScope scope, Guid tenantId)
    {
        // INV-4: tenant_id divergente é sempre negado.
        if (principal.TenantId != tenantId)
            return AuthorizationResult.Denied();

        return principal.Role switch
        {
            GoalRole.TenantAdmin =>
                // TenantAdmin lê metas do tenant inteiro.
                AuthorizationResult.Allowed,

            GoalRole.Executivo =>
                // Executivo lê metas do tenant inteiro (leitura, não escrita).
                AuthorizationResult.Allowed,

            GoalRole.GestorDeBu =>
                // GestorDeBu lê somente metas da sua BU.
                principal.BuId == scope.BuId
                    ? AuthorizationResult.Allowed
                    : AuthorizationResult.Denied(),

            GoalRole.Vendedor =>
                // Vendedor lê apenas metas onde é o owner_id.
                scope.OwnerId.HasValue && scope.OwnerId.Value == principal.UserId
                    ? AuthorizationResult.Allowed
                    : AuthorizationResult.Denied(),

            _ => AuthorizationResult.Denied()
        };
    }
}
