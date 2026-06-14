namespace ActivityManagement.Domain.Activities.Specifications;

/// <summary>
/// Specification que filtra atividades conforme o papel do usuário autenticado.
/// Regras (Req 13):
///   - Vendedor: vê apenas as próprias atividades (owner_id) dentro do tenant.
///   - Gestor de BU: vê todas as atividades da BU dentro do tenant.
/// Sempre restrito ao tenant autenticado (RNF 1, ADR-0001).
/// A verificação de tenant nesta specification é uma defesa adicional —
/// o filtro global do EF Core e o RLS do PostgreSQL aplicam o tenant antes (DD-002).
/// Mapeia: design §4.6, Req 13, RNF 1, TASK-05.
/// </summary>
public sealed class ScopeSpecification
{
    private readonly Guid  _tenantId;
    private readonly Guid? _ownerId; // nulo quando papel=Gestor (vê toda a BU)
    private readonly Guid? _buId;    // nulo quando papel=Vendedor

    private ScopeSpecification(Guid tenantId, Guid? ownerId, Guid? buId)
    {
        _tenantId = tenantId;
        _ownerId  = ownerId;
        _buId     = buId;
    }

    /// <summary>
    /// Cria a specification para um Vendedor: vê apenas as próprias atividades.
    /// </summary>
    /// <param name="tenantId">Tenant do usuário autenticado.</param>
    /// <param name="ownerId">Identificador do Vendedor autenticado.</param>
    public static ScopeSpecification ForSeller(Guid tenantId, Guid ownerId)
        => new(tenantId, ownerId, buId: null);

    /// <summary>
    /// Cria a specification para um Gestor de BU: vê todas as atividades da BU.
    /// </summary>
    /// <param name="tenantId">Tenant do usuário autenticado.</param>
    /// <param name="buId">Business Unit do gestor.</param>
    public static ScopeSpecification ForManager(Guid tenantId, Guid buId)
        => new(tenantId, ownerId: null, buId);

    /// <summary>
    /// Avalia se a atividade está dentro do escopo do usuário autenticado.
    /// </summary>
    /// <param name="activity">Atividade a avaliar.</param>
    /// <returns>
    /// <c>true</c> quando a atividade pertence ao tenant e está dentro do escopo de papel do usuário.
    /// </returns>
    public bool IsSatisfiedBy(Activity activity)
    {
        // Sempre restrito ao tenant (defesa adicional)
        if (activity.TenantId != _tenantId)
            return false;

        // Vendedor: apenas as próprias
        if (_ownerId.HasValue)
            return activity.OwnerId == _ownerId.Value;

        // Gestor de BU: todas as da BU
        if (_buId.HasValue)
            return activity.BuId == _buId.Value;

        return false;
    }
}
