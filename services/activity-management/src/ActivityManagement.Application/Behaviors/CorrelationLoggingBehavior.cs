namespace ActivityManagement.Application.Behaviors;

using ActivityManagement.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging;

/// <summary>
/// Pipeline behavior MediatR (1ª posição — design §5.4).
/// Injeta <c>correlation_id</c>, <c>tenant_id</c> e <c>user_id</c> no escopo de log estruturado.
/// Nunca loga <c>title</c> nem <c>description</c> (campos de texto livre com PII potencial — RNF 7.2).
/// Mapeia: RNF 6.1, RNF 7.2, design §5.4, TASK-06.
/// </summary>
/// <typeparam name="TRequest">Tipo do Command ou Query MediatR.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class CorrelationLoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<CorrelationLoggingBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Inicializa o behavior com o logger estruturado.
    /// </summary>
    public CorrelationLoggingBehavior(
        ILogger<CorrelationLoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 cancellationToken)
    {
        var requestName  = typeof(TRequest).Name;
        var correlationId = Guid.NewGuid();
        Guid? tenantId   = null;
        Guid? userId     = null;

        if (request is ITenantRequest tenantRequest && tenantRequest.TenantContext is not null)
        {
            correlationId = tenantRequest.TenantContext.CorrelationId;
            tenantId      = tenantRequest.TenantContext.TenantId;
            userId        = tenantRequest.TenantContext.UserId;
        }

        // IMPORTANTE: nunca incluir title, description ou campos de texto livre neste log (RNF 7.2).
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlation_id"] = correlationId,
            ["tenant_id"]      = tenantId,
            ["user_id"]        = userId,
            ["request_name"]   = requestName,
        }))
        {
            _logger.LogInformation(
                "Iniciando {RequestName} correlation_id={CorrelationId}",
                requestName,
                correlationId);

            try
            {
                var response = await next(cancellationToken);

                _logger.LogInformation(
                    "Concluído {RequestName} correlation_id={CorrelationId}",
                    requestName,
                    correlationId);

                return response;
            }
            catch (Exception ex)
            {
                // Nunca logar a mensagem de exceção com conteúdo de atividade (RNF 7.3)
                _logger.LogError(
                    ex,
                    "Erro em {RequestName} correlation_id={CorrelationId} tipo={ExceptionType}",
                    requestName,
                    correlationId,
                    ex.GetType().Name);

                throw;
            }
        }
    }
}
