using DataMigration.Application.Ports;
using Microsoft.EntityFrameworkCore.Storage;

namespace DataMigration.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IUnitOfWork"/> compartilhada entre todos os
/// adaptadores de porta de import (DD-001, design §6.1).
///
/// Garante que todo o import roda em uma única transação PostgreSQL.
/// Dry-run usa <see cref="BeginRollbackOnlyAsync"/> — a transação é sempre revertida.
///
/// Rastreia: design §6.1, DD-001, Req 6, RNF 5, TASK-15, TASK-18.
/// </summary>
internal sealed class SharedUnitOfWork : IUnitOfWork
{
    private readonly MigrationDbContext _context;
    private IDbContextTransaction? _transaction;

    public SharedUnitOfWork(MigrationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public bool IsRollbackOnly { get; private set; }

    /// <inheritdoc />
    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("Transação já iniciada.");
        }

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        IsRollbackOnly = false;
    }

    /// <inheritdoc />
    public async Task BeginRollbackOnlyAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("Transação já iniciada.");
        }

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        IsRollbackOnly = true;
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("Nenhuma transação ativa.");
        }

        if (IsRollbackOnly)
        {
            // Transação rollback-only nunca commita (dry-run, Req 2.1).
            await RollbackAsync(cancellationToken);
            return;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _transaction.CommitAsync(cancellationToken);
        await DisposeTransactionAsync();
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await DisposeTransactionAsync();
    }

    private async Task DisposeTransactionAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
