using GoalForecast.Application.Common;
using MediatR;
using AppException = GoalForecast.Application.Common.ApplicationException;

namespace GoalForecast.Application.Behaviors;

/// <summary>
/// Behavior 2/5: injeta/valida o tenant_id do principal no contexto do request.
/// Lança <see cref="AppException"/> (GF-ERR-006, 403) se tenant_id não estiver presente
/// ou for Guid.Empty (ADR-0001, camada de aplicação).
///
/// Atua apenas em requests que implementam <see cref="IHasPrincipal"/>.
/// Mapeia: ADR-0001, design §5.4, TASK-10.
/// </summary>
public sealed class TenantContextBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IHasPrincipal hasPrincipal)
        {
            var tenantId = hasPrincipal.Principal.TenantId;
            if (tenantId == Guid.Empty)
                throw new AppException("GF-ERR-006",
                    "Operação não permitida: tenant_id ausente ou inválido no contexto autenticado.",
                    403);
        }

        return await next();
    }
}
