using DataMigration.Application.Ports;
using DataMigration.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace DataMigration.Infrastructure.Persistence;

/// <summary>
/// Implementação EF Core de <see cref="IMigrationJobRepository"/>.
///
/// Todas as operações são escopadas ao tenant corrente via Global Query Filter (ADR-0001).
///
/// Rastreia: design §6.1, TASK-15.
/// </summary>
internal sealed class MigrationJobRepository : IMigrationJobRepository
{
    private readonly MigrationDbContext _context;

    public MigrationJobRepository(MigrationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(MigrationJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        await _context.MigrationJobs.AddAsync(job, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MigrationJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.MigrationJobs
            .Include(j => j.LogEntries)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(MigrationJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        _context.MigrationJobs.Update(job);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
