using Microsoft.AspNetCore.Authorization;

namespace Digest.Api.Infrastructure;

/// <summary>
/// Nome canônico da política de autorização do trigger (design §10, DIG-ERR-011).
/// </summary>
public static class TriggerAuthorizationPolicy
{
    /// <summary>Nome da política usada em <c>RequireAuthorization</c>.</summary>
    public const string Name = "DigestTrigger.SchedulerOnly";
}

/// <summary>
/// Requisito de autorização: o caller deve ser a service account autorizada do Cloud Scheduler.
/// Identidade diferente → 403 DIG-ERR-011 (design §10, RNF 7.1).
/// A mensagem de erro não revela quais identidades são válidas (anti-enumeração — design §12).
/// </summary>
/// <param name="AuthorizedEmail">E-mail da SA autorizada.</param>
public sealed record SchedulerServiceAccountRequirement(string AuthorizedEmail)
    : IAuthorizationRequirement;

/// <summary>
/// Handler de autorização que verifica o claim <c>email</c> do token OIDC.
/// </summary>
public sealed class SchedulerServiceAccountHandler
    : AuthorizationHandler<SchedulerServiceAccountRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SchedulerServiceAccountRequirement requirement)
    {
        // Extrai claim de e-mail do token OIDC do GCP
        var emailClaim = context.User.FindFirst("email")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

        if (string.Equals(emailClaim, requirement.AuthorizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
        else
        {
            // Falha: identidade não autorizada → 403 DIG-ERR-011
            // Não registrar o e-mail inválido no log (anti-enumeração, RNF 3)
            context.Fail();
        }

        return Task.CompletedTask;
    }
}
