using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Reporting.Application.Behaviors;

/// <summary>
/// Behavior 5: emite logs estruturados e métricas por geração de relatório.
///
/// Regras de PII (DD-008, RNF 4.2):
/// <list type="bullet">
///   <item><description>NUNCA loga <c>display_name</c>, e-mail, telefone ou valores de outro tenant.</description></item>
///   <item><description>Loga apenas: <c>correlation_id</c>, <c>tenant_id</c>, <c>request_type</c>, <c>duration_ms</c>, <c>outcome</c>.</description></item>
/// </list>
///
/// Mapeia: TASK-12, design §5.4, §11, RNF 6, DD-008, RNF 4.2.
/// </summary>
public sealed class LoggingMetricsBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingMetricsBehavior<TRequest, TResponse>> _logger;

    /// <summary>Inicializa com o logger.</summary>
    public LoggingMetricsBehavior(ILogger<LoggingMetricsBehavior<TRequest, TResponse>> logger)
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
        var requestType   = typeof(TRequest).Name;
        var correlationId = request is IReportingQuery rq ? rq.CorrelationId : "none";
        var tenantId      = request is IReportingQuery rq2 ? rq2.TenantId.ToString() : "unknown";

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();

            // Log sem PII — apenas campos operacionais (RNF 4.2, RNF 6.1)
            _logger.LogInformation(
                "Relatório gerado: {RequestType} | correlation_id={CorrelationId} | tenant_id={TenantId} | duration_ms={DurationMs} | outcome=success",
                requestType, correlationId, tenantId, sw.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();

            // Log de erro sem PII — sem dados do tenant/usuário além do correlation_id (RNF 4.2)
            _logger.LogError(ex,
                "Falha ao gerar relatório: {RequestType} | correlation_id={CorrelationId} | tenant_id={TenantId} | duration_ms={DurationMs} | outcome=error",
                requestType, correlationId, tenantId, sw.ElapsedMilliseconds);

            throw;
        }
    }
}
