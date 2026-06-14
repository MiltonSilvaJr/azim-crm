using MediatR;
using Microsoft.Extensions.Logging;
using Organization.Application.Abstractions;
using Organization.Application.Ports;

namespace Organization.Application.Behaviors;

/// <summary>
/// Pipeline behavior de transação.
/// Envolve apenas commands (<see cref="ICommand"/> e <see cref="ICommand{TResult}"/>) em uma transação.
/// Garante que o agregado e a tabela <c>outbox_events</c> são commitados atomicamente (Req 3.3, §6.6).
/// Falha no handler provoca rollback automático.
/// </summary>
/// <typeparam name="TRequest">Tipo do request.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IDatabaseContext _databaseContext;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    /// <summary>Inicializa o behavior com o contexto de banco.</summary>
    public TransactionBehavior(
        IDatabaseContext databaseContext,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _databaseContext = databaseContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Somente commands usam transação; queries não transacionam
        var isCommand = request is ICommand || IsGenericCommand(request.GetType());
        if (!isCommand)
            return await next(cancellationToken);

        _logger.LogDebug("Iniciando transação para {RequestType}", typeof(TRequest).Name);

        await _databaseContext.BeginTransactionAsync(cancellationToken);
        try
        {
            var response = await next(cancellationToken);
            await _databaseContext.CommitTransactionAsync(cancellationToken);

            _logger.LogDebug("Transação commitada para {RequestType}", typeof(TRequest).Name);
            return response;
        }
        catch
        {
            _logger.LogWarning("Rollback da transação para {RequestType}", typeof(TRequest).Name);
            await _databaseContext.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private static bool IsGenericCommand(Type type)
    {
        return type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));
    }
}
