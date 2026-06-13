namespace TenantAdministration.Application.Authorization;

/// <summary>
/// Marca um command/query como restrito ao plano de plataforma (Platform Operator).
/// O <see cref="TenantAdministration.Application.Behaviors.AuthorizationBehavior{TRequest,TResponse}"/>
/// rejeita qualquer request marcado com este atributo quando o ator não for PlatOp.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequiresPlatformOperatorAttribute : Attribute { }
