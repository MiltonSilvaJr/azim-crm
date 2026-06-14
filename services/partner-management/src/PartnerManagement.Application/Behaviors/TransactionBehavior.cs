using MediatR;

namespace PartnerManagement.Application.Behaviors;

/// <summary>
/// Porta de unidade de trabalho transacional.
/// Implementada na Infrastructure; usada pelo <see cref="TransactionBehavior{TRequest, TResponse}"/>.
/// Garante atomicidade: escrita de domínio + Outbox + auditoria na mesma transação (design §6.6).
/// Mapeia: design §5.4, design §6.6, RNF 2.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Inicia uma transação.</summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Confirma a transação.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Desfaz a transação.</summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Behavior MediatR (posição 5 no pipeline) que abre transação para Commands.
/// Queries não são envolvidas em transação.
/// Em Commands, coleta domain events no Outbox e faz commit atômico (design §5.4, §6.6).
/// Mapeia: RNF 2, design §5.4.
/// </summary>
/// <typeparam name="TRequest">Tipo do request MediatR.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class TransactionBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Apenas Commands são envolvidos em transação
        // (Queries são somente-leitura)
        bool isCommand = typeof(TRequest).Name.EndsWith("Command", StringComparison.Ordinal);

        if (!isCommand)
        {
            return await next().ConfigureAwait(false);
        }

        await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            TResponse response = await next().ConfigureAwait(false);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return response;
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
