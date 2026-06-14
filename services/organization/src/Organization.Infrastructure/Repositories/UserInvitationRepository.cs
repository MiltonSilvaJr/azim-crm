using Microsoft.EntityFrameworkCore;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Organization.Infrastructure.Persistence;

namespace Organization.Infrastructure.Repositories;

/// <summary>
/// Implementação concreta de <see cref="IUserInvitationRepository"/> usando EF Core.
/// O isolamento por tenant é garantido pelo global query filter do <see cref="OrganizationDbContext"/>.
/// </summary>
public sealed class UserInvitationRepository : IUserInvitationRepository
{
    private readonly OrganizationDbContext _context;

    /// <summary>Inicializa o repositório com o contexto de banco.</summary>
    public UserInvitationRepository(OrganizationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task SaveAsync(UserInvitation invitation, CancellationToken cancellationToken = default)
    {
        var existing = await _context.UserInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invitation.Id, cancellationToken);

        if (existing is null)
            _context.UserInvitations.Add(invitation);
        else
            _context.UserInvitations.Update(invitation);
    }

    /// <inheritdoc/>
    public async Task<UserInvitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invitation = await _context.UserInvitations
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invitation is not null)
            _context.RestoreTargetMemberships(invitation);

        return invitation;
    }

    /// <inheritdoc/>
    public async Task<UserInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        // i.TokenHash é um value object (InvitationToken) com HasConversion(t => t.TokenHash, ...).
        // EF Core não traduz i.TokenHash.TokenHash em SQL porque não consegue navegar para
        // dentro do objeto convertido. Comparar com o objeto de valor completo usa o converter
        // (InvitationToken -> string) e é traduzido corretamente.
        var token = InvitationToken.FromHash(tokenHash);
        var invitation = await _context.UserInvitations
            .FirstOrDefaultAsync(i => i.TokenHash == token, cancellationToken);

        if (invitation is not null)
            _context.RestoreTargetMemberships(invitation);

        return invitation;
    }

    /// <inheritdoc/>
    public async Task<bool> IsEmailActiveUserAsync(string email, CancellationToken cancellationToken = default)
    {
        // Verifica se existe usuário ativo com o e-mail informado no tenant corrente.
        // Utilizado para bloquear convite duplicado (ORG-ERR-003) sem revelar existência de conta.
        return await _context.Users
            .AnyAsync(u => u.Active && u.Email == email, cancellationToken);
    }
}
