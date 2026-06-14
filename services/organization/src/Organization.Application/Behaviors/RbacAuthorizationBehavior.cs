using MediatR;
using Microsoft.Extensions.Logging;
using Organization.Application.Abstractions;
using Organization.Application.Ports;

namespace Organization.Application.Behaviors;

/// <summary>
/// Pipeline behavior de autorização RBAC deny-by-default.
/// Avalia o atributo <see cref="RequiresRoleAttribute"/> no request antes do handler.
/// Ausência do atributo implica negação (deny-by-default, RNF 2, Req 6).
/// </summary>
/// <typeparam name="TRequest">Tipo do request.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class RbacAuthorizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<RbacAuthorizationBehavior<TRequest, TResponse>> _logger;

    /// <summary>Inicializa o behavior com o contexto de tenant/usuário autenticado.</summary>
    public RbacAuthorizationBehavior(
        ITenantContext tenantContext,
        ILogger<RbacAuthorizationBehavior<TRequest, TResponse>> logger)
    {
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest);
        var attribute = (RequiresRoleAttribute?)Attribute.GetCustomAttribute(requestType, typeof(RequiresRoleAttribute));

        // Aceite de convite: sem JWT — não passa por RBAC
        if (attribute is { AllowAnonymous: true })
            return await next(cancellationToken);

        // Deny-by-default: sem atributo = acesso negado
        if (attribute is null)
        {
            _logger.LogWarning(
                "Acesso negado por deny-by-default: request {RequestType} não possui RequiresRoleAttribute. ORG-ERR-010",
                requestType.Name);
            throw new UnauthorizedAccessException(
                $"Você não tem permissão para esta ação. ORG-ERR-010 [{requestType.Name}]");
        }

        var userRoles = _tenantContext.RolesByBu.Values.Distinct().ToHashSet(StringComparer.Ordinal);
        var hasRole = attribute.Roles.Any(r => userRoles.Contains(r));

        if (!hasRole)
        {
            _logger.LogWarning(
                "Acesso negado: usuário {UserId} não possui papel autorizado para {RequestType}. ORG-ERR-010",
                _tenantContext.UserId,
                requestType.Name);
            throw new UnauthorizedAccessException(
                $"Você não tem permissão para esta ação. ORG-ERR-010 [{requestType.Name}]");
        }

        return await next(cancellationToken);
    }
}
