namespace AuditLog.Application.Abstractions;

/// <summary>
/// Fornece informações do usuário autenticado corrente para decisões de autorização (REQ-008).
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Papel RBAC do usuário autenticado.
    /// Retorna <see langword="null"/> quando não há contexto de autenticação.
    /// </summary>
    string? Role { get; }
}

/// <summary>Papéis RBAC conhecidos pelo módulo de auditoria.</summary>
public static class AuditRoles
{
    /// <summary>Tenant Admin — acessa a trilha completa do tenant (REQ-008.2).</summary>
    public const string TenantAdmin = "TenantAdmin";

    /// <summary>Gestor de BU — acessa apenas registros das suas BUs (REQ-008.3, DD-008).</summary>
    public const string GestorBU = "GestorBU";
}
