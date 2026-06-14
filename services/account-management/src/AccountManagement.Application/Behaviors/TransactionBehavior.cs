using MediatR;

namespace AccountManagement.Application.Behaviors;

/// <summary>
/// Behavior MediatR que envolve commands em uma transação de banco de dados.
///
/// Posição no pipeline: 5ª (após <see cref="PiiAccessBehavior{TRequest,TResponse}"/>
/// e antes do handler) — design §5.4.
///
/// Comportamento:
/// - Para <see cref="ICommand"/> (Commands com IRequest): abre transação, executa handler,
///   comita em caso de sucesso, faz rollback em caso de exceção.
/// - Para Queries: passa direto (sem transação).
///
/// A transação garante que a escrita de domínio e a gravação no Outbox são atômicas (DD-007).
/// A implementação concreta de transação vive na Infrastructure — este behavior apenas
/// chama o port <see cref="IUnitOfWork"/>.
///
/// Mapeia: design §5.4, DD-007, Req 8.
/// </summary>
internal sealed class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Inicializa o behavior com o unit of work injetado.</summary>
    public TransactionBehavior(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Somente Commands executam dentro de transação; Queries passam diretamente.
        if (request is not ICommand)
            return await next(cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next(cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

/// <summary>
/// Marcador para Commands que devem ser executados dentro de uma transação.
/// Implementado por todos os <c>IRequest</c> e <c>IRequest&lt;T&gt;</c> de escrita.
/// </summary>
public interface ICommand
{
}
