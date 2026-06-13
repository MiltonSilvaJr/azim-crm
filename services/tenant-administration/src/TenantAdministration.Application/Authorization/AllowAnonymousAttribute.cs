namespace TenantAdministration.Application.Authorization;

/// <summary>
/// Marca um command/query como público (sem exigência de autenticação de plano).
/// Usado pelo endpoint <c>brand.json</c> e outros recursos de acesso anônimo.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AllowAnonymousAttribute : Attribute { }
