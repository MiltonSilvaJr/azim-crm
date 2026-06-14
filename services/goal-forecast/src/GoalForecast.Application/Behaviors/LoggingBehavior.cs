using MediatR;
using Microsoft.Extensions.Logging;

namespace GoalForecast.Application.Behaviors;

/// <summary>
/// Behavior 1/5: logs estruturados no início e fim de cada request MediatR.
/// Emite: <c>correlation_id</c>, <c>tenant_id</c>, <c>bu_id</c>, request type e duração (RNF 7.1).
/// Não expõe valorMeta nem dados sensíveis nos logs (RNF-7.3).
///
/// Posição no pipeline: primeiro (envolve todos os demais behaviors).
/// Mapeia: RNF 7.1, RNF-7.3, design §5.4, TASK-10.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Campos de log estruturado canônicos (design §5.4, RNF 7.1).
    internal const string FieldCorrelationId = "correlation_id";
    internal const string FieldTenantId = "tenant_id";
    internal const string FieldBuId = "bu_id";
    internal const string FieldRequestType = "request_type";
    internal const string FieldDurationMs = "duration_ms";

    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    /// <summary>Inicializa o behavior com o logger.</summary>
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest).Name;
        var correlationId = Guid.NewGuid().ToString(); // Gerado aqui se não vier do contexto HTTP.

        string? tenantId = null;
        string? buId = null;

        if (request is IHasPrincipal hasPrincipal)
        {
            tenantId = hasPrincipal.Principal.TenantId.ToString();
            buId = hasPrincipal.Principal.BuId?.ToString();
        }

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            [FieldCorrelationId] = correlationId,
            [FieldTenantId] = tenantId,
            [FieldBuId] = buId,
            [FieldRequestType] = requestType
        });

        _logger.LogInformation("Iniciando {RequestType}", requestType);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var response = await next();
            sw.Stop();
            _logger.LogInformation("Concluído {RequestType} em {DurationMs}ms",
                requestType, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Falha em {RequestType} após {DurationMs}ms",
                requestType, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
