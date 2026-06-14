using MediatR;
using PartnerManagement.Application.Partners;
using PartnerManagement.Application.Ports;

namespace PartnerManagement.Application.Behaviors;

/// <summary>
/// Behavior MediatR (posição 4 no pipeline) que verifica RBAC por permissão do JWT.
/// Se o request decorado com <see cref="RequiredPermissionAttribute"/> exigir permissão
/// que o usuário não possui, lança <see cref="AccessDeniedException"/> (PM-ERR-008).
/// Mapeia: RNF 1, design §5.4, rule jwt-permissions.md.
/// </summary>
/// <typeparam name="TRequest">Tipo do request MediatR.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class AuthorizationBehavior<TRequest, TResponse>(
    IPermissionContext permissionContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Verifica atributo de permissão requerida no tipo do request
        RequiredPermissionAttribute? attr = typeof(TRequest)
            .GetCustomAttributes(typeof(RequiredPermissionAttribute), inherit: false)
            .OfType<RequiredPermissionAttribute>()
            .FirstOrDefault();

        if (attr is not null && !permissionContext.HasPermission(attr.Permission))
        {
            throw new AccessDeniedException(attr.Permission);
        }

        return next();
    }
}

/// <summary>
/// Exceção lançada quando o usuário não possui permissão para executar a operação (PM-ERR-008).
/// </summary>
public sealed class AccessDeniedException(string requiredPermission)
    : Exception($"Acesso negado. Permissão requerida: {requiredPermission}.")
{
    /// <summary>Permissão que foi requerida e não estava presente no token.</summary>
    public string RequiredPermission { get; } = requiredPermission;
}
