using Microsoft.EntityFrameworkCore;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Organization.Infrastructure.Persistence;

namespace Organization.Infrastructure.Repositories;

/// <summary>
/// Implementação concreta de <see cref="IUserRepository"/> usando EF Core.
/// O isolamento por tenant é garantido pelo global query filter do <see cref="OrganizationDbContext"/>.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly OrganizationDbContext _context;

    /// <summary>Inicializa o repositório com o contexto de banco.</summary>
    public UserRepository(OrganizationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task SaveAsync(User user, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);

        if (existing is null)
            _context.Users.Add(user);
        else
            _context.Users.Update(user);
    }

    /// <inheritdoc/>
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Memberships)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Memberships)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<User>> GetActiveTenantAdminsAsync(CancellationToken cancellationToken = default)
    {
        // m.Role é um value object com HasConversion(r => r.Value, ...).
        // EF Core não traduz m.Role.Value em SQL; comparar m.Role == Role.TAdmin usa o converter
        // e é traduzido corretamente para role = 'TAdmin'.
        return await _context.Users
            .Include(u => u.Memberships)
            .Where(u => u.Active && u.Memberships.Any(m => m.Role == Role.TAdmin))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<User>> ListActiveAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var skip = (page - 1) * pageSize;
        return await _context.Users
            .Include(u => u.Memberships)
            .Where(u => u.Active)
            .OrderBy(u => u.Email)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }
}
