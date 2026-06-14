namespace ActivityManagement.Application.Behaviors;

using ActivityManagement.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging;

/// <summary>
/// Exceção lançada pelo <c>TenantScopeBehavior</c> quando o <see cref="TenantContext"/>
/// não está presente no request (falha-fechada — RNF 1, DD-002).
/// </summary>
public sealed class TenantContextMissingException : Exception
{
    /// <summary>Inicializa a exceção com mensagem padrão.</summary>
    public TenantContextMissingException()
        : base("TenantContext ausente no request. Toda operação requer contexto de tenant autenticado.")
    {
    }
}

/// <summary>
/// Pipeline behavior MediatR (2ª posição — design §5.4).
/// Garante que toda operação opera no tenant autenticado (RNF 1, DD-002).
/// Falha-fechada: rejeita o request quando <see cref="TenantContext"/> está ausente,
/// sem propagar para o handler.
/// Mapeia: RNF 1, DD-002, ADR-0001, design §5.4, TASK-06.
/// </summary>
/// <typeparam name="TRequest">Tipo do Command ou Query MediatR que implementa <see cref="ITenantRequest"/>.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class TenantScopeBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<TenantScopeBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Inicializa o behavior com logger para registro de rejeições.
    /// </summary>
    public TenantScopeBehavior(ILogger<TenantScopeBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 cancellationToken)
    {
        // Apenas requests que implementam ITenantRequest são sujeitos à verificação.
        // Requests sem marcador (ex: ProcessDigestActionCommand — autoridade via token) passam diretamente.
        if (request is ITenantRequest tenantRequest)
        {
            if (tenantRequest.TenantContext is null)
            {
                _logger.LogWarning(
                    "TenantContext ausente para {RequestType} — request rejeitado (falha-fechada, RNF 1).",
                    typeof(TRequest).Name);

                throw new TenantContextMissingException();
            }
        }

        return await next(cancellationToken);
    }
}
