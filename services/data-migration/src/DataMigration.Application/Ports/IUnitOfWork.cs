namespace DataMigration.Application.Ports;

/// <summary>
/// Porta de unidade de trabalho (Unit of Work) compartilhada entre as portas
/// de import dos módulos-alvo.
///
/// Garante que todo o import roda em uma única transação PostgreSQL (DD-001,
/// RN-023, design §5.3, §6.1). O dry-run usa <see cref="BeginRollbackOnlyAsync"/>
/// para transação marcada para descarte incondicional.
///
/// Implementação em Infrastructure (EF Core DbContext / transação Postgres).
///
/// Rastreia: design §5.3, §6.1, DD-001, Req 6, RNF 5, TASK-09, TASK-11.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Inicia uma transação real que será commitada em sucesso ou revertida em falha.
    /// Usada no <c>ExecuteImportHandler</c> (tudo-ou-nada, RN-023).
    /// </summary>
    Task BeginAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inicia uma transação marcada para rollback incondicional.
    /// Usada no <c>RunDryRunHandler</c> (simulação sem efeitos colaterais, Req 2.1).
    /// </summary>
    Task BeginRollbackOnlyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commita a transação atual (somente para transações normais).
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverte a transação atual. Para transações rollback-only, é chamado automaticamente.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica se a transação atual é marcada para rollback incondicional.
    /// </summary>
    bool IsRollbackOnly { get; }
}
