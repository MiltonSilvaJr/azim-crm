using MediatR;
using Microsoft.Extensions.Options;

namespace Reporting.Application.Behaviors;

/// <summary>
/// Opções de timeout para queries de relatório.
/// Configurável via <c>appsettings.json</c> — nunca hardcoded (design §5.4, RISK-REPORT-01).
/// </summary>
public sealed class QueryTimeoutOptions
{
    /// <summary>Seção de configuração.</summary>
    public const string Section = "Reporting:QueryTimeout";

    /// <summary>
    /// Timeout em segundos para queries de relatório.
    /// Default: 4 segundos (design §6.1, RISK-REPORT-01).
    /// </summary>
    public int TimeoutSeconds { get; init; } = 4;
}

/// <summary>
/// Behavior 6: aplica timeout configurável às queries de relatório.
///
/// Protege o banco transacional de queries lentas (RISK-REPORT-01, RNF 1.3).
/// O timeout é configurável — nunca hardcoded.
/// Lança <see cref="TimeoutException"/> quando excedido (→ <c>REPORT-ERR-008</c> na API).
///
/// Mapeia: TASK-12, design §5.4, §6.1, RNF 1.3, RISK-REPORT-01.
/// </summary>
public sealed class QueryTimeoutBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly QueryTimeoutOptions _options;

    /// <summary>Inicializa com as opções de timeout.</summary>
    public QueryTimeoutBehavior(IOptions<QueryTimeoutOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <summary>
    /// Timeout configurado em segundos.
    /// Exposto para testes de verificação do valor aplicado.
    /// </summary>
    public int TimeoutSeconds => _options.TimeoutSeconds;

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            return await next().WaitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Query {typeof(TRequest).Name} excedeu o timeout de {_options.TimeoutSeconds}s. " +
                $"(REPORT-ERR-008, RISK-REPORT-01, RNF 1.3)");
        }
    }
}
