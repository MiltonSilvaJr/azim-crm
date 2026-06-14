namespace Organization.Application.Abstractions;

/// <summary>
/// Atributo aplicado a commands/queries para declarar os papéis autorizados.
/// Avaliado pelo <c>RbacAuthorizationBehavior</c> antes da execução do handler.
/// Deny-by-default: a ausência deste atributo nega a execução (RNF 2, Req 6).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequiresRoleAttribute : Attribute
{
    /// <summary>Papéis autorizados para a operação.</summary>
    public IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Quando <c>true</c>, o acesso é verificado somente para a BU especificada no command.
    /// Usado por <c>GestorBU</c> que só pode operar na própria BU.
    /// </summary>
    public bool ScopedToBu { get; init; }

    /// <summary>
    /// Quando <c>true</c>, o command não requer autenticação por JWT (ex.: aceite de convite por token).
    /// </summary>
    public bool AllowAnonymous { get; init; }

    /// <summary>
    /// Inicializa o atributo com os papéis autorizados.
    /// </summary>
    /// <param name="roles">Papéis permitidos (ex.: "TAdmin", "GestorBU").</param>
    public RequiresRoleAttribute(params string[] roles)
    {
        Roles = roles;
    }
}
