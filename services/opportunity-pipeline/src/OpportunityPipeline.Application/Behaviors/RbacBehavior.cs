using MediatR;
using OpportunityPipeline.Application.Common;

namespace OpportunityPipeline.Application.Behaviors;

/// <summary>
/// Pipeline behavior: valida política RBAC do command.
/// Executa TERCEIRO — retorna 403 antes de tocar domínio ou banco.
/// Lê o atributo [RequiresRole] aplicado à classe do command.
/// Mapeia: RNF 4 (RBAC por endpoint), design §5.4 posição 3, design §10.
/// </summary>
public sealed class RbacBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requiresRole = typeof(TRequest)
            .GetCustomAttributes(typeof(RequiresRoleAttribute), inherit: false)
            .Cast<RequiresRoleAttribute>()
            .FirstOrDefault();

        // Se não há atributo, operação não exige RBAC (ex.: queries somente-leitura abertas)
        if (requiresRole is null)
            return next(cancellationToken);

        if (request is not IAuthenticatedCommand authCommand)
            return next(cancellationToken);

        var userRole = authCommand.UserRole;
        var allowed = requiresRole.AllowedRoles;

        if (!allowed.Contains(userRole))
            throw new ForbiddenException(userRole, allowed);

        return next(cancellationToken);
    }
}
