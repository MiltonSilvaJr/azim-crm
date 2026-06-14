namespace ActivityManagement.Application.Behaviors;

using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Events;
using MediatR;
using Microsoft.Extensions.Logging;

/// <summary>
/// Abstração do repositório transacional para o <c>TransactionBehavior</c>.
/// Permite que o behavior colete domain events e faça commit atômico
/// sem depender de EF Core diretamente na camada Application.
/// Implementado em Infrastructure pelo <c>ActivityManagementDbContext</c>.
/// Mapeia: design §5.4, §6.5, TASK-06.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Salva as mudanças pendentes e persiste os domain events no Outbox na mesma transação.</summary>
    /// <param name="domainEvents">Events acumulados pelos agregados modificados nesta operação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task CommitAsync(IReadOnlyList<DomainEvent> domainEvents, CancellationToken cancellationToken = default);
}

/// <summary>
/// Marcador para Commands que requerem transaction + Outbox (escritas de negócio).
/// Queries não implementam este marcador e passam sem abertura de transação.
/// Mapeia: design §5.4, TASK-06.
/// </summary>
public interface ITransactionalCommand
{
    /// <summary>
    /// Domain events acumulados pelos agregados modificados por este command.
    /// Preenchido pelo handler antes do commit do <c>TransactionBehavior</c>.
    /// </summary>
    IReadOnlyList<DomainEvent> DomainEvents { get; }
}

/// <summary>
/// Pipeline behavior MediatR (5ª posição — design §5.4).
/// Abre transação para Commands e faz commit atômico de escrita de domínio + Outbox + auditoria.
/// Queries passam diretamente sem overhead transacional.
/// Mapeia: design §5.4, §6.5, §6.6, TASK-06.
/// </summary>
/// <typeparam name="TRequest">Tipo do Command ou Query MediatR.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IUnitOfWork                                          _unitOfWork;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>>    _logger;

    /// <summary>
    /// Inicializa o behavior com a unit of work e o logger.
    /// </summary>
    public TransactionBehavior(
        IUnitOfWork                                        unitOfWork,
        ILogger<TransactionBehavior<TRequest, TResponse>>  logger)
    {
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 cancellationToken)
    {
        // Queries não são transacionais
        if (request is not ITransactionalCommand)
            return await next(cancellationToken);

        _logger.LogDebug("Abrindo transação para {RequestType}.", typeof(TRequest).Name);

        var response = await next(cancellationToken);

        // Após o handler, coleta domain events e faz commit atômico
        var transactionalCommand = (ITransactionalCommand)request;
        await _unitOfWork.CommitAsync(transactionalCommand.DomainEvents, cancellationToken);

        _logger.LogDebug("Transação confirmada para {RequestType}.", typeof(TRequest).Name);

        return response;
    }
}
