namespace ActivityManagement.Application.Common;

/// <summary>
/// Contexto de tenant resolvido do token JWT para a requisição corrente.
/// Preenchido pelo <c>TenantScopeBehavior</c> e propagado via <c>IRequest</c>.
/// Mapeia: design §5.4, RNF 1, DD-002, ADR-0001.
/// </summary>
/// <param name="TenantId">Identificador do tenant autenticado.</param>
/// <param name="BuId">Identificador da Business Unit do usuário autenticado.</param>
/// <param name="UserId">Identificador do usuário autenticado.</param>
/// <param name="Role">Papel do usuário: <c>seller</c>, <c>bu_manager</c> ou <c>viewer</c>.</param>
/// <param name="CorrelationId">Identificador de correlação da requisição (RNF 6.1).</param>
public sealed record TenantContext(
    Guid   TenantId,
    Guid   BuId,
    Guid   UserId,
    string Role,
    Guid   CorrelationId);
