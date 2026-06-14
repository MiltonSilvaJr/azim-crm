using Digest.Application.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Digest.Application.Behaviors;

/// <summary>
/// Pipeline behavior que loga início e fim de commands/queries.
/// Loga apenas <c>correlation_id</c>, <c>tenant_id</c>, nome do request e duração — sem PII (RNF 3, RNF 6.1, DD-011).
/// Endereço de e-mail, conteúdo do digest e títulos de atividades nunca são logados.
/// </summary>
/// <remarks>
/// Identifica campos PII pelo tipo do request e os mascara/omite explicitamente.
/// </remarks>
public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Constrói o behavior com o logger injetado.
    /// </summary>
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
        var requestName = typeof(TRequest).Name;
        var (tenantId, correlationId) = ExtractSafeContext(request);

        _logger.LogInformation(
            "Iniciando {RequestName} | tenant_id={TenantId} correlation_id={CorrelationId}",
            requestName, tenantId, correlationId);

        var start = DateTimeOffset.UtcNow;
        try
        {
            var response = await next();

            _logger.LogInformation(
                "Concluído {RequestName} | tenant_id={TenantId} correlation_id={CorrelationId} duration_ms={DurationMs}",
                requestName, tenantId, correlationId,
                (DateTimeOffset.UtcNow - start).TotalMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Falha em {RequestName} | tenant_id={TenantId} correlation_id={CorrelationId} duration_ms={DurationMs}",
                requestName, tenantId, correlationId,
                (DateTimeOffset.UtcNow - start).TotalMilliseconds);
            throw;
        }
    }

    // ------------------------------------------------------------------
    // Extrai apenas campos seguros — sem PII (RNF 3, DD-011)
    // ------------------------------------------------------------------

    private static (Guid? tenantId, Guid? correlationId) ExtractSafeContext(TRequest request) =>
        request switch
        {
            RunDigestForTenantCommand cmd => (cmd.TenantId, cmd.CorrelationId),
            SendUserDigestCommand cmd => (cmd.TenantId, cmd.CorrelationId),
            // UserEmail e conteúdo NUNCA são extraídos — PII
            _ => (null, null),
        };
}
