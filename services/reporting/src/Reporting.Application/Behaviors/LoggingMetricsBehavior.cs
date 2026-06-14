using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Reporting.Application.Observability;

namespace Reporting.Application.Behaviors;

/// <summary>
/// Behavior 5: emite logs estruturados e métricas por geração de relatório.
///
/// Regras de PII (DD-008, RNF 4.2):
/// <list type="bullet">
///   <item><description>NUNCA loga <c>display_name</c>, e-mail, telefone ou valores de outro tenant.</description></item>
///   <item><description>Loga apenas: <c>correlation_id</c>, <c>tenant_id</c>, <c>request_type</c>, <c>duration_ms</c>, <c>outcome</c>.</description></item>
///   <item><description><c>filters_hash</c> substituído pelo nome do tipo de request — nunca valores brutos de filtros.</description></item>
/// </list>
///
/// Métricas emitidas via <see cref="ReportingMetrics"/> (design §11, RNF 6.2):
/// <list type="bullet">
///   <item><description><c>reports_generated_total{report_type,outcome}</c></description></item>
///   <item><description><c>report_generation_duration_seconds{report_type}</c></description></item>
/// </list>
///
/// Mapeia: TASK-12, TASK-24, design §5.4, §11, RNF 6, DD-008, RNF 4.2.
/// </summary>
public sealed class LoggingMetricsBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingMetricsBehavior<TRequest, TResponse>> _logger;
    private readonly ReportingMetrics _metrics;

    /// <summary>Inicializa com o logger e as métricas do módulo reporting.</summary>
    public LoggingMetricsBehavior(
        ILogger<LoggingMetricsBehavior<TRequest, TResponse>> logger,
        ReportingMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(metrics);
        _logger  = logger;
        _metrics = metrics;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestType   = typeof(TRequest).Name;
        var correlationId = request is IReportingQuery rq ? rq.CorrelationId
                          : request is IScopedQuery sq0    ? sq0.Scope.TenantId.ToString("N")
                          : "none";
        var tenantId      = request is IReportingQuery rq2 ? rq2.TenantId.ToString()
                          : request is IScopedQuery sq1     ? sq1.Scope.TenantId.ToString()
                          : "unknown";

        // report_type em snake_case para métricas (design §11, RNF 6.2)
        var reportType = ToSnakeCase(requestType);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();

            var durationSeconds = sw.Elapsed.TotalSeconds;

            // Log sem PII — apenas campos operacionais (RNF 4.2, RNF 6.1)
            // Nunca loga: display_name, e-mail, telefone, valores de filtro brutos
            _logger.LogInformation(
                "Relatório gerado: {RequestType} | correlation_id={CorrelationId} | tenant_id={TenantId} | duration_ms={DurationMs} | outcome=success",
                requestType, correlationId, tenantId, sw.ElapsedMilliseconds);

            // Métricas: incrementa contador e histograma de latência (RNF 6.2)
            _metrics.RecordReportGenerated(reportType, "success", durationSeconds);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();

            var durationSeconds = sw.Elapsed.TotalSeconds;

            // Log de erro sem PII (RNF 4.2) — sem dados do tenant/usuário além do correlation_id
            _logger.LogError(ex,
                "Falha ao gerar relatório: {RequestType} | correlation_id={CorrelationId} | tenant_id={TenantId} | duration_ms={DurationMs} | outcome=error",
                requestType, correlationId, tenantId, sw.ElapsedMilliseconds);

            // Métricas de erro (RNF 6.2)
            _metrics.RecordReportGenerated(reportType, "error", durationSeconds);

            throw;
        }
    }

    /// <summary>
    /// Converte o nome do tipo de request para snake_case para uso como label de métrica.
    /// Exemplo: "GetFunnelReportQuery" → "get_funnel_report_query".
    /// </summary>
    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var result = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0)
            {
                result.Append('_');
            }
            result.Append(char.ToLowerInvariant(c));
        }
        return result.ToString();
    }
}
