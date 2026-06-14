using Digest.Application.Behaviors;

namespace Digest.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IUnitOfWork"/> sobre o <see cref="DigestDbContext"/>.
/// Chamada pelo <c>UnitOfWorkBehavior</c> para persisitir alterações de <c>EmailDigestLog</c> + Outbox
/// na mesma transação (DD-009, design §5.4).
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly DigestDbContext _context;

    /// <summary>Constrói a unidade de trabalho com o DbContext injetado.</summary>
    public UnitOfWork(DigestDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
