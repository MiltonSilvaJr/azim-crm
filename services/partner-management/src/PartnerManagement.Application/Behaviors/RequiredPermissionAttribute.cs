namespace PartnerManagement.Application.Behaviors;

/// <summary>
/// Atributo que marca um Command ou Query com a permissão RBAC mínima necessária.
/// O <see cref="AuthorizationBehavior{TRequest, TResponse}"/> lê este atributo para decidir
/// se o usuário autenticado possui o acesso requerido.
/// Mapeia: RNF 1, design §5.4, rule jwt-permissions.md.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequiredPermissionAttribute(string permission) : Attribute
{
    /// <summary>Permissão exigida para executar o request.</summary>
    public string Permission { get; } = permission;
}
