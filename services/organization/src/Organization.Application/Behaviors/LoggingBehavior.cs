using MediatR;
using Microsoft.Extensions.Logging;
using Organization.Application.Ports;

namespace Organization.Application.Behaviors;

/// <summary>
/// Pipeline behavior de logging estruturado.
/// Registra início e fim de cada request com <c>correlation_id</c> e <c>tenant_id</c>.
/// Nunca loga e-mail, <c>display_name</c> nem qualquer PII (RNF 3.1, RNF 6.1).
/// </summary>
/// <typeparam name="TRequest">Tipo do request.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    /// <summary>Inicializa o behavior com o contexto de tenant.</summary>
    public LoggingBehavior(
        ITenantContext tenantContext,
        ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var tenantId = _tenantContext.TenantId;
        var userId = _tenantContext.UserId;
        var correlationId = _tenantContext.CorrelationId;

        // Loga sem PII: usa apenas identificadores opacos (UUIDs)
        _logger.LogInformation(
            "Executando {RequestType} | TenantId={TenantId} | UserId={UserId} | CorrelationId={CorrelationId}",
            requestName,
            tenantId,
            userId,
            correlationId);

        try
        {
            var response = await next(cancellationToken);

            _logger.LogInformation(
                "Concluído {RequestType} | TenantId={TenantId} | CorrelationId={CorrelationId}",
                requestName,
                tenantId,
                correlationId);

            return response;
        }
        catch (Exception ex)
        {
            // Não loga a mensagem de exceção completa — pode conter PII
            _logger.LogWarning(
                "Falha em {RequestType} | TenantId={TenantId} | CorrelationId={CorrelationId} | ExceptionType={ExceptionType}",
                requestName,
                tenantId,
                correlationId,
                ex.GetType().Name);
            throw;
        }
    }
}
