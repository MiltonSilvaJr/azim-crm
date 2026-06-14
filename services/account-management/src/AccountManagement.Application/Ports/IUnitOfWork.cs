namespace AccountManagement.Application.Ports;

/// <summary>
/// Porta de saída para controle de transação de banco de dados.
///
/// Usada pelo <see cref="AccountManagement.Application.Behaviors.TransactionBehavior{TRequest,TResponse}"/>
/// para garantir atomicidade entre a escrita de domínio e a gravação no Outbox (DD-007).
///
/// Implementação concreta na Infrastructure com EF Core DbContext.
///
/// Mapeia: design §5.4, design §6.6, DD-007.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Inicia uma nova transação de banco de dados.</summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Comita a transação corrente.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Faz rollback da transação corrente.</summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
