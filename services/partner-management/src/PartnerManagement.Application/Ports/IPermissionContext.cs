namespace PartnerManagement.Application.Ports;

/// <summary>
/// Porta que expõe as permissões RBAC do usuário autenticado extraídas do JWT.
/// Implementada na Infrastructure resolvendo o claim <c>permissions</c> do token.
/// Mapeia: RNF 1, design §5.4, rule jwt-permissions.md.
/// </summary>
public interface IPermissionContext
{
    /// <summary>Lista de permissões do usuário autenticado (claim <c>permissions</c>).</summary>
    IReadOnlyList<string> Permissions { get; }

    /// <summary>Verifica se o usuário possui a permissão requerida.</summary>
    bool HasPermission(string permission);
}

/// <summary>
/// Permissões canônicas do módulo partner-management.
/// Mapeia: RNF 1.1..1.3, design §10.
/// </summary>
public static class PartnerPermissions
{
    /// <summary>Criar e editar parceiros (Tenant Admin e Gestor de BU).</summary>
    public const string Write = "partners:write";

    /// <summary>Inativar/reativar parceiros (Tenant Admin).</summary>
    public const string Manage = "partners:manage";

    /// <summary>Listar e visualizar parceiros (Viewer+).</summary>
    public const string Read = "partners:read";

    /// <summary>Acessar relatório de comissões (Tenant Admin e Gestor de BU).</summary>
    public const string ReadCommissions = "partners:commissions:read";
}
