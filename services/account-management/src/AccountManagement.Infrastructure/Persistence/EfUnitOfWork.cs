using AccountManagement.Application.Ports;
using Microsoft.EntityFrameworkCore.Storage;

namespace AccountManagement.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IUnitOfWork"/> usando EF Core.
///
/// Garante atomicidade entre a escrita de domínio e a gravação no Outbox (DD-007).
/// Usada pelo <c>TransactionBehavior</c> para gerenciar a transação do request HTTP.
///
/// Mapeia: design §5.4, IUnitOfWork, DD-007, TASK-10.
/// </summary>
internal sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly AccountManagementDbContext _context;
    private IDbContextTransaction? _transaction;

    public EfUnitOfWork(AccountManagementDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException(
                "Não é possível fazer commit sem uma transação ativa. Chame BeginTransactionAsync primeiro.");

        await _context.SaveChangesAsync(cancellationToken);
        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
