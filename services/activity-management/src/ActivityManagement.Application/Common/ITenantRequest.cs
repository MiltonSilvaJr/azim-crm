namespace ActivityManagement.Application.Common;

/// <summary>
/// Marcador para Commands e Queries que carregam <see cref="TenantContext"/>.
/// O <c>TenantScopeBehavior</c> usa este marcador para injetar o contexto resolvido do JWT.
/// Mapeia: design §5.4, RNF 1, TASK-06.
/// </summary>
public interface ITenantRequest
{
    /// <summary>Contexto de tenant resolvido do token JWT. Preenchido pelo <c>TenantScopeBehavior</c>.</summary>
    TenantContext? TenantContext { get; set; }
}
