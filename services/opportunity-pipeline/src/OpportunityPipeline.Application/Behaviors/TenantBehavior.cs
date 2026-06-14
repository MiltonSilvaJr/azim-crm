using MediatR;
using OpportunityPipeline.Application.Common;

namespace OpportunityPipeline.Application.Behaviors;

/// <summary>
/// Pipeline behavior: resolve TenantContext a partir do JWT da request.
/// Executa SEGUNDO no pipeline — garante tenant_id antes de qualquer acesso a dados.
/// Mapeia: ADR-0001 (isolamento multi-tenant), RNF 3, design §5.4 posição 2.
/// </summary>
public sealed class TenantBehavior<TRequest, TResponse>(
    TenantContext tenantContext,
    ITenantResolver tenantResolver)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.IsInitialized)
        {
            var (tenantId, buId, actorId, role) = await tenantResolver
                .ResolveAsync(cancellationToken)
                .ConfigureAwait(false);

            tenantContext.Initialize(tenantId, buId, actorId);

            // Injeta papel no request se suportar
            if (request is IMutableAuthenticatedCommand mutable)
                mutable.SetRole(role);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Porta de resolução do contexto de tenant a partir do JWT.
/// Implementação na camada Api (extrai claims do HttpContext).
/// Mapeia: design §5.4, ADR-0001.
/// </summary>
public interface ITenantResolver
{
    /// <summary>Resolve tenant_id, bu_id, actor_id e papel do usuário autenticado.</summary>
    Task<(Guid TenantId, Guid BuId, Guid ActorId, UserRole Role)> ResolveAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface para commands que aceitam injeção de papel pelo TenantBehavior.
/// </summary>
public interface IMutableAuthenticatedCommand : IAuthenticatedCommand
{
    void SetRole(UserRole role);
}
