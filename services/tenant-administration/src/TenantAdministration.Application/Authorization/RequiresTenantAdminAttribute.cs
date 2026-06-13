namespace TenantAdministration.Application.Authorization;

/// <summary>
/// Marca um command/query como restrito ao plano de tenant (Tenant Admin).
/// O <see cref="TenantAdministration.Application.Behaviors.AuthorizationBehavior{TRequest,TResponse}"/>
/// rejeita qualquer request marcado com este atributo quando o ator não for TAdmin.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequiresTenantAdminAttribute : Attribute { }
