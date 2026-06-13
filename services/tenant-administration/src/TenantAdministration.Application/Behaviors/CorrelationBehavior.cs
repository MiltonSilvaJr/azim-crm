using MediatR;
using Microsoft.Extensions.Logging;
using TenantAdministration.Application.Ports;

namespace TenantAdministration.Application.Behaviors;

/// <summary>
/// Behavior MediatR #1 na cadeia do pipeline (design.md §5.4).
/// Propaga <c>correlation_id</c> e <c>tenant_id</c> para o contexto de log estruturado (Serilog LogContext).
/// Garante rastreabilidade de toda operação conforme ADR-0009 e RNF 6.
/// </summary>
public sealed class CorrelationBehavior<TRequest, TResponse>(
    ITenantContext tenantContext,
    ILogger<CorrelationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var correlationId = tenantContext.CorrelationId ?? Guid.NewGuid().ToString("N");
        var tenantId = tenantContext.TenantId?.ToString() ?? "platform";
        var slug = tenantContext.Slug ?? "platform";

        // Simula injeção no LogContext (Serilog). Em produção usa LogContext.PushProperty.
        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlation_id"] = correlationId,
            ["tenant_id"] = tenantId,
            ["slug"] = slug,
            ["request_type"] = typeof(TRequest).Name
        }))
        {
            return await next(cancellationToken);
        }
    }
}
