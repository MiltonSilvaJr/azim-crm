namespace GoalForecast.Domain.Authorization;

/// <summary>
/// Papéis de usuário do módulo goal-forecast, conforme a matriz RBAC (design §10, Req 12).
/// </summary>
public enum GoalRole
{
    /// <summary>Administrador do tenant: pode criar/editar metas em qualquer BU e ler o tenant.</summary>
    TenantAdmin,

    /// <summary>Gestor de BU: pode criar/editar metas na sua BU e ler metas da sua BU.</summary>
    GestorDeBu,

    /// <summary>Executivo: pode ler metas do tenant; não pode criar/editar.</summary>
    Executivo,

    /// <summary>Vendedor: pode ler apenas as próprias metas (owner_id == UserId).</summary>
    Vendedor
}
