using AuditLog.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuditLog.Application.Behaviors;

/// <summary>
/// Behavior de pipeline MediatR que emite logs estruturados sem PII para toda requisição (design §5.4, RNF-002).
/// Posição na pipeline: 4ª (mais interna, antes do handler).
/// <para>
/// Campos registrados em log (RNF-002.1):
/// <list type="bullet">
/// <item><description><c>RequestType</c>: nome da classe da requisição.</description></item>
/// <item><description><c>TenantId</c>: do contexto autenticado.</description></item>
/// <item><description><c>EntityType</c>: quando disponível via <see cref="IEntityContextRequest"/>.</description></item>
/// <item><description><c>EntityId</c>: quando disponível via <see cref="IEntityContextRequest"/>.</description></item>
/// </list>
/// Nunca registra: PII, delta_json, valores de negócio sensíveis.
/// </para>
/// </summary>
/// <typeparam name="TRequest">Tipo da requisição MediatR.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta MediatR.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    /// <summary>Inicializa o behavior com suas dependências.</summary>
    public LoggingBehavior(
        ITenantContext tenantContext,
        ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(logger);

        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest).Name;
        var tenantId = _tenantContext.TenantId;

        // Extrai contexto de entidade sem expor PII (RNF-002.1)
        string? entityType = null;
        Guid? entityId = null;

        if (request is IEntityContextRequest entityCtx)
        {
            entityType = entityCtx.EntityType;
            entityId = entityCtx.EntityId;
        }

        _logger.LogInformation(
            "Iniciando {RequestType} | TenantId={TenantId} | EntityType={EntityType} | EntityId={EntityId}",
            requestType,
            tenantId,
            entityType ?? "(nenhum)",
            entityId?.ToString() ?? "(nenhum)");

        try
        {
            var response = await next(cancellationToken);

            _logger.LogInformation(
                "Concluído {RequestType} | TenantId={TenantId} | Outcome=Success",
                requestType,
                tenantId);

            return response;
        }
        catch (Exception ex)
        {
            // Log de falha sem expor detalhes de payload ou PII (RNF-002.3)
            _logger.LogError(ex,
                "Falha em {RequestType} | TenantId={TenantId} | ExceptionType={ExceptionType}",
                requestType,
                tenantId,
                ex.GetType().Name);

            throw;
        }
    }
}
