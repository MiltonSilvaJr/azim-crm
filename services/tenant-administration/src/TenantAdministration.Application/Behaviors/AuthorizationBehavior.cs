using MediatR;
using TenantAdministration.Application.Authorization;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;

namespace TenantAdministration.Application.Behaviors;

/// <summary>
/// Behavior MediatR #4 na cadeia do pipeline (design.md §5.4, RNF 7).
/// Verifica o papel do ator (PlatformOperator vs TenantAdmin/Viewer) com base nos atributos
/// <see cref="RequiresPlatformOperatorAttribute"/>, <see cref="RequiresTenantAdminAttribute"/>
/// e <see cref="AllowAnonymousAttribute"/> no tipo do request.
/// PlatOp é bloqueado em rotas de tenant; TAdmin é bloqueado em rotas de plataforma.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(
    ICurrentUserContext userContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest);

        // Acesso anônimo permitido explicitamente
        if (requestType.IsDefined(typeof(AllowAnonymousAttribute), inherit: false))
            return await next(cancellationToken);

        // Rota restrita a Platform Operator
        if (requestType.IsDefined(typeof(RequiresPlatformOperatorAttribute), inherit: false))
        {
            if (!userContext.IsPlatformOperator)
                throw new AuthorizationException(
                    "Acesso negado: esta operação é restrita ao Platform Operator.");

            return await next(cancellationToken);
        }

        // Rota restrita a Tenant Admin
        if (requestType.IsDefined(typeof(RequiresTenantAdminAttribute), inherit: false))
        {
            if (userContext.IsPlatformOperator)
                throw new AuthorizationException(
                    "Acesso negado: Platform Operator não pode executar operações no plano de tenant (RNF 7).");

            if (!userContext.IsTenantAdmin)
                throw new AuthorizationException(
                    "Acesso negado: esta operação exige o papel de Tenant Admin.");

            return await next(cancellationToken);
        }

        // Sem atributo de autorização: apenas usuários autenticados (Viewer ou superior)
        if (!userContext.IsPlatformOperator && !userContext.IsTenantAdmin && !userContext.IsViewer)
            throw new AuthorizationException(
                "Acesso negado: autenticação exigida.");

        return await next(cancellationToken);
    }
}
