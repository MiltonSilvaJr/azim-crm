using MediatR;
using Microsoft.Extensions.Logging;

namespace Reporting.Application.Behaviors;

/// <summary>
/// Behavior 1: propaga <c>correlation_id</c> e <c>tenant_id</c> no escopo de log e trace.
///
/// Posição na pipeline: primeiro (antes de qualquer verificação).
/// Responsabilidade: inicializar o contexto de rastreabilidade da requisição.
///
/// Mapeia: TASK-12, design §5.4, ADR-0001, RNF 6.
/// </summary>
public sealed class CorrelationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<CorrelationBehavior<TRequest, TResponse>> _logger;

    /// <summary>Inicializa com o logger.</summary>
    public CorrelationBehavior(ILogger<CorrelationBehavior<TRequest, TResponse>> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var correlationId = request is IReportingQuery rq ? rq.CorrelationId : Guid.NewGuid().ToString();
        var tenantId      = request is IReportingQuery rq2 ? rq2.TenantId.ToString() : "unknown";

        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["correlation_id"] = correlationId,
                   ["tenant_id"]      = tenantId,
                   ["request_type"]   = typeof(TRequest).Name
               }))
        {
            return await next();
        }
    }
}
