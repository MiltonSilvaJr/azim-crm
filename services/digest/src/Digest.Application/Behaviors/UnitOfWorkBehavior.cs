using MediatR;

namespace Digest.Application.Behaviors;

/// <summary>
/// Porta de abstração de unidade de trabalho para o pipeline behavior.
/// Implementação concreta em <c>Digest.Infrastructure</c> (TASK-14).
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persiste todas as alterações pendentes na transação corrente.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Pipeline behavior que encerra a transação que abrange <c>EmailDigestLog</c> + Outbox (DD-009, design §5.4).
/// Garante que o registro de envio e o evento de auditoria são atômicos (RNF 10).
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Constrói o behavior com a unidade de trabalho injetada.
    /// </summary>
    public UnitOfWorkBehavior(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();

        // Commit somente após o handler concluir com sucesso
        await _unitOfWork.CommitAsync(cancellationToken);

        return response;
    }
}
