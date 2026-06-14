using GoalForecast.Application.Commands;
using GoalForecast.Application.Common;
using GoalForecast.Domain.Policies;
using GoalForecast.Domain.ValueObjects;
using MediatR;
using AppException = GoalForecast.Application.Common.ApplicationException;

namespace GoalForecast.Application.Behaviors;

/// <summary>
/// Behavior 4/5: aplica <see cref="GoalAuthorizationPolicy"/> para commands de escrita.
/// Nega operação com GF-ERR-006 (403) sem revelar existência de metas fora do escopo (RNF-2.3).
///
/// Atua apenas em requests que implementam <see cref="IHasPrincipal"/>.
/// A lógica de autorização fina (BU específica, escopo) é delegada ao handler;
/// este behavior verifica a camada de autorização de nível de command.
///
/// Mapeia: RNF 2, design §5.4, TASK-10.
/// </summary>
public sealed class GoalAuthorizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Apenas valida que existe um principal com tenant_id válido.
        // A autorização fina (BU, scope) é feita no handler pelo GoalAuthorizationPolicy.
        if (request is IHasPrincipal hasPrincipal)
        {
            var principal = hasPrincipal.Principal;
            if (principal.TenantId == Guid.Empty)
                throw new AppException("GF-ERR-006",
                    "Operação não permitida.", 403);
        }

        return await next();
    }
}
