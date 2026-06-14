using MediatR;
using Microsoft.Extensions.Logging;
using OpportunityPipeline.Application.Common;
using System.Diagnostics;

namespace OpportunityPipeline.Application.Behaviors;

/// <summary>
/// Pipeline behavior: registra correlação, tenant, tipo de comando e duração.
/// Executa PRIMEIRO no pipeline.
/// Nunca loga campos PII (RNF 10).
/// Mapeia: RNF 10 (observabilidade sem PII), design §5.4 posição 1.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    TenantContext tenantContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var commandType = typeof(TRequest).Name;
        var correlationId = (request is IAuthenticatedCommand auth) ? auth.CorrelationId : "unknown";

        var sw = Stopwatch.StartNew();

        logger.LogInformation(
            "Command iniciado. command_type={CommandType} correlation_id={CorrelationId}",
            commandType,
            correlationId);

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);

            sw.Stop();

            // Loga tenant_id apenas quando o contexto já foi inicializado (TenantBehavior pode vir depois em mock)
            if (tenantContext.IsInitialized)
            {
                logger.LogInformation(
                    "Command concluído. command_type={CommandType} correlation_id={CorrelationId} tenant_id={TenantId} elapsed_ms={ElapsedMs}",
                    commandType,
                    correlationId,
                    tenantContext.TenantId,
                    sw.ElapsedMilliseconds);
            }
            else
            {
                logger.LogInformation(
                    "Command concluído. command_type={CommandType} correlation_id={CorrelationId} elapsed_ms={ElapsedMs}",
                    commandType,
                    correlationId,
                    sw.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(
                ex,
                "Command falhou. command_type={CommandType} correlation_id={CorrelationId} elapsed_ms={ElapsedMs} error={ErrorType}",
                commandType,
                correlationId,
                sw.ElapsedMilliseconds,
                ex.GetType().Name);
            throw;
        }
    }
}
