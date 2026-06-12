using AuditLog.Application.Abstractions;
using AuditLog.Application.Errors;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuditLog.Application.Behaviors;

/// <summary>
/// Behavior de pipeline MediatR que garante a presença de <c>tenant_id</c> no contexto autenticado
/// antes de qualquer handler de command ou query (design §5.4, DD-007).
/// Posição na pipeline: 2ª — depois de <see cref="ValidationBehavior{TRequest,TResponse}"/>.
/// <para>
/// Quando <c>tenant_id</c> está ausente:
/// <list type="number">
/// <item><description>Incrementa o contador <c>audit_query_without_tenant_context_total</c> (DD-007).</description></item>
/// <item><description>Lança <see cref="AuditAuthorizationException"/> com código <see cref="AuditErrorCodes.TenantContextMissing"/>.</description></item>
/// </list>
/// </para>
/// </summary>
/// <typeparam name="TRequest">Tipo da requisição MediatR.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta MediatR.</typeparam>
public sealed class TenantContextBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ITenantContext _tenantContext;
    private readonly IAuditMetrics _metrics;
    private readonly ILogger<TenantContextBehavior<TRequest, TResponse>> _logger;

    /// <summary>Inicializa o behavior com suas dependências.</summary>
    public TenantContextBehavior(
        ITenantContext tenantContext,
        IAuditMetrics metrics,
        ILogger<TenantContextBehavior<TRequest, TResponse>> logger)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);

        _tenantContext = tenantContext;
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
        {
            _metrics.IncrementQueryWithoutTenantContext();

            _logger.LogWarning(
                "Requisição {RequestType} bloqueada: tenant_id ausente no contexto (DD-007). " +
                "Possível falha de configuração ou autenticação.",
                typeof(TRequest).Name);

            throw new AuditAuthorizationException(
                AuditErrorCodes.TenantContextMissing,
                "Contexto de tenant ausente. A requisição requer um contexto autenticado com tenant_id válido.");
        }

        return await next(cancellationToken);
    }
}
