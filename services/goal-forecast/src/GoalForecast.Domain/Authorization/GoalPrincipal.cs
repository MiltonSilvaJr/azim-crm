namespace GoalForecast.Domain.Authorization;

/// <summary>
/// Representa o principal autenticado no contexto de autorização de metas.
/// Imutável (record). Construído a partir do JWT pelo pipeline de comportamentos.
/// TenantId e UserId são sempre extraídos do token, nunca do payload (Req 12.1).
///
/// Mapeia: Req 12, RNF 2, design §10, TASK-07.
/// </summary>
/// <param name="TenantId">Tenant do principal (do token JWT).</param>
/// <param name="UserId">Identificador do usuário (do token JWT).</param>
/// <param name="Role">Papel do usuário no sistema.</param>
/// <param name="BuId">BU do principal; obrigatório para GestorDeBu; nulo para outros papéis.</param>
public sealed record GoalPrincipal(
    Guid TenantId,
    Guid UserId,
    GoalRole Role,
    Guid? BuId);
