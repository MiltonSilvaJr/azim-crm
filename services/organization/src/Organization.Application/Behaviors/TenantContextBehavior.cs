using MediatR;
using Microsoft.Extensions.Logging;
using Organization.Application.Abstractions;
using Organization.Application.Ports;

namespace Organization.Application.Behaviors;

/// <summary>
/// Pipeline behavior que garante o contexto de tenant antes de qualquer handler.
/// Executa <c>SET app.current_tenant</c> na conexão de banco para ativar o RLS (DEC-006, ADR-0001).
/// Rejeita requests sem <c>tenant_id</c> válido.
/// </summary>
/// <typeparam name="TRequest">Tipo do request.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class TenantContextBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ITenantContext _tenantContext;
    private readonly IDatabaseContext _databaseContext;
    private readonly ILogger<TenantContextBehavior<TRequest, TResponse>> _logger;

    /// <summary>Inicializa o behavior com os ports de contexto de tenant e banco.</summary>
    public TenantContextBehavior(
        ITenantContext tenantContext,
        IDatabaseContext databaseContext,
        ILogger<TenantContextBehavior<TRequest, TResponse>> logger)
    {
        _tenantContext = tenantContext;
        _databaseContext = databaseContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Requests anônimos (AllowAnonymous = true) não possuem tenant_id — passam sem contexto de tenant.
        // Exemplo: AcceptInvitationCommand, ListStagesQuery (endpoints públicos, design §9).
        var requestType = typeof(TRequest);
        var requiresRoleAttr = (RequiresRoleAttribute?)Attribute.GetCustomAttribute(requestType, typeof(RequiresRoleAttribute));
        if (requiresRoleAttr is { AllowAnonymous: true })
            return await next(cancellationToken);

        var tenantId = _tenantContext.TenantId;

        if (tenantId == Guid.Empty)
            throw new InvalidOperationException(
                "Contexto de tenant ausente. O request deve ser processado com tenant_id válido.");

        _logger.LogDebug(
            "Definindo contexto de tenant {TenantId} para request {RequestType}",
            tenantId,
            typeof(TRequest).Name);

        await _databaseContext.SetTenantAsync(tenantId, cancellationToken);

        return await next(cancellationToken);
    }
}
