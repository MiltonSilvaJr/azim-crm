using MediatR;
using Microsoft.Extensions.Logging;

namespace AccountManagement.Application.Behaviors;

/// <summary>
/// Behavior MediatR que injeta contexto de rastreabilidade no escopo de log.
///
/// Posição no pipeline: 1ª (antes de todos os outros behaviors) — design §5.4.
///
/// Responsabilidades:
/// - Injetar <c>correlation_id</c>, <c>tenant_id</c> e tipo do request no escopo do log.
/// - Registrar início e fim de cada request com duração.
/// - NUNCA registrar PII (nome, e-mail, telefone de contato) em texto claro (RNF 1, RNF 9).
///
/// Mapeia: design §5.4, RNF 1, RNF 9, rule observability.md.
/// </summary>
internal sealed class CorrelationLoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<CorrelationLoggingBehavior<TRequest, TResponse>> _logger;
    private readonly CorrelationContext _correlationContext;

    /// <summary>Inicializa o behavior com logger e contexto de correlação.</summary>
    public CorrelationLoggingBehavior(
        ILogger<CorrelationLoggingBehavior<TRequest, TResponse>> logger,
        CorrelationContext correlationContext)
    {
        _logger = logger;
        _correlationContext = correlationContext;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest).Name;
        var correlationId = _correlationContext.CorrelationId ?? "N/A";
        var tenantId = _correlationContext.TenantId?.ToString() ?? "N/A";

        // Log de início — sem PII (RNF 1.1)
        _logger.LogInformation(
            "Iniciando {RequestType} | correlation_id={CorrelationId} tenant_id={TenantId}",
            requestType,
            correlationId,
            tenantId);

        var start = DateTimeOffset.UtcNow;

        try
        {
            var response = await next(cancellationToken);

            var elapsed = DateTimeOffset.UtcNow - start;
            _logger.LogInformation(
                "Concluído {RequestType} em {ElapsedMs}ms | correlation_id={CorrelationId}",
                requestType,
                elapsed.TotalMilliseconds,
                correlationId);

            return response;
        }
        catch (Exception ex)
        {
            var elapsed = DateTimeOffset.UtcNow - start;
            // Mensagem de erro sem PII (RNF 1.3)
            _logger.LogError(
                "Falha em {RequestType} após {ElapsedMs}ms | correlation_id={CorrelationId} | {ExceptionType}",
                requestType,
                elapsed.TotalMilliseconds,
                correlationId,
                ex.GetType().Name);

            throw;
        }
    }
}
