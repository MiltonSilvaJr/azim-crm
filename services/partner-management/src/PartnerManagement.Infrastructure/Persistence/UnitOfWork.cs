using Microsoft.EntityFrameworkCore.Storage;
using PartnerManagement.Application.Behaviors;

namespace PartnerManagement.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IUnitOfWork"/> sobre o <see cref="PartnerManagementDbContext"/>.
/// Garante atomicidade: escrita de domínio + Outbox + auditoria na mesma transação (design §6.6).
/// Mapeia: design §5.4, design §6.6, RNF 2, TASK-18.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly PartnerManagementDbContext _dbContext;
    private IDbContextTransaction? _transaction;

    /// <summary>
    /// Inicializa a unidade de trabalho com o DbContext scoped.
    /// </summary>
    /// <param name="dbContext">DbContext da requisição corrente.</param>
    public UnitOfWork(PartnerManagementDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (_transaction is not null)
        {
            await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
