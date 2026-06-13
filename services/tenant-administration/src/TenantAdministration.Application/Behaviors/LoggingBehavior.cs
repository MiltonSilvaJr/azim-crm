using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace TenantAdministration.Application.Behaviors;

/// <summary>
/// Behavior MediatR #2 na cadeia do pipeline (design.md §5.4).
/// Registra log estruturado de entrada e saída de cada request com duração e resultado.
/// Obrigatório conforme RNF 6 (observabilidade).
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        logger.LogInformation("Iniciando {RequestName}", requestName);

        try
        {
            var response = await next(cancellationToken);
            sw.Stop();

            logger.LogInformation(
                "Concluído {RequestName} em {ElapsedMs}ms",
                requestName,
                sw.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(
                ex,
                "Falha em {RequestName} após {ElapsedMs}ms",
                requestName,
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}
