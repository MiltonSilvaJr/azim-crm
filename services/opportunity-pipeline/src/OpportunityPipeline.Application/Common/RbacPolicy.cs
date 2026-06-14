namespace OpportunityPipeline.Application.Common;

/// <summary>
/// Define os papéis permitidos para uma operação.
/// Aplicado via atributo nos commands para o RbacBehavior.
/// Mapeia: RNF 4 (RBAC), design §5.4, §10.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequiresRoleAttribute : Attribute
{
    /// <summary>Papéis permitidos para a operação.</summary>
    public UserRole[] AllowedRoles { get; }

    /// <param name="allowedRoles">Um ou mais papéis com acesso autorizado.</param>
    public RequiresRoleAttribute(params UserRole[] allowedRoles)
    {
        if (allowedRoles is null || allowedRoles.Length == 0)
            throw new ArgumentException("Pelo menos um papel deve ser informado.", nameof(allowedRoles));

        AllowedRoles = allowedRoles;
    }
}

/// <summary>
/// Papéis de usuário do sistema.
/// Mapeia: design §10 (matriz papel × capacidade).
/// </summary>
public enum UserRole
{
    /// <summary>Vendedor — cria/edita/move/ganha/perde oportunidades da própria BU.</summary>
    Vendedor,

    /// <summary>Gestor de BU — todas as operações de Vendedor + reabertura.</summary>
    GestorBU,

    /// <summary>Admin de tenant — todas as operações, qualquer BU.</summary>
    TenantAdmin,

    /// <summary>Visualizador — somente leitura.</summary>
    Viewer
}

/// <summary>
/// Exceção lançada pelo RbacBehavior quando papel do usuário não está autorizado.
/// Mapeia a HTTP 403 no middleware de erros.
/// Mapeia: RNF 4, OP-ERR-008.
/// </summary>
public sealed class ForbiddenException : Exception
{
    /// <summary>Papel atual do usuário.</summary>
    public UserRole UserRole { get; }

    /// <summary>Papéis requeridos pela operação.</summary>
    public UserRole[] RequiredRoles { get; }

    /// <param name="userRole">Papel atual do usuário que tentou a operação.</param>
    /// <param name="requiredRoles">Papéis mínimos para a operação.</param>
    public ForbiddenException(UserRole userRole, UserRole[] requiredRoles)
        : base($"Papel '{userRole}' não autorizado para esta operação. Papéis permitidos: {string.Join(", ", requiredRoles)}")
    {
        UserRole = userRole;
        RequiredRoles = requiredRoles;
    }
}
