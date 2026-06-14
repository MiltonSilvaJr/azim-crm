using MediatR;
using PartnerManagement.Application.Ports;

namespace PartnerManagement.Application.Behaviors;

/// <summary>
/// Behavior MediatR (posição 2 no pipeline) que verifica se o tenant foi resolvido corretamente.
/// Rejeita requisições sem tenant_id válido com <see cref="UnauthorizedTenantException"/>.
/// O TenantContext é resolvido pelo middleware de TenantResolution na API e injetado via DI.
/// Mapeia: RNF 1, DD-001, design §5.4.
/// </summary>
/// <typeparam name="TRequest">Tipo do request MediatR.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class TenantScopeBehavior<TRequest, TResponse>(
    ITenantContext tenantContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.IsResolved)
        {
            throw new UnauthorizedTenantException();
        }

        return next();
    }
}

/// <summary>
/// Exceção lançada quando o tenant não pôde ser resolvido da requisição corrente.
/// Mapeia: RNF 1, design §5.4.
/// </summary>
public sealed class UnauthorizedTenantException()
    : Exception("Tenant não resolvido. Acesso negado.")
{
}
