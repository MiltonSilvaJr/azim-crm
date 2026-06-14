using MediatR;
using Organization.Application.Ports;
using Organization.Domain.Exceptions;

namespace Organization.Application.Commands.Invitation;

/// <summary>
/// Handler para <see cref="RevokeInvitationCommand"/>.
/// Revoga convite em estado <c>Pending</c>; estados terminais retornam ORG-ERR-006.
/// </summary>
public sealed class RevokeInvitationCommandHandler : IRequestHandler<RevokeInvitationCommand>
{
    private readonly IUserInvitationRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    /// <summary>Inicializa o handler.</summary>
    public RevokeInvitationCommandHandler(
        IUserInvitationRepository repository,
        ITenantContext tenantContext,
        IClock clock)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task Handle(RevokeInvitationCommand request, CancellationToken cancellationToken)
    {
        var invitation = await _repository.GetByIdAsync(request.InvitationId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Convite '{request.InvitationId}' não encontrado.");

        // Delega ao domínio; DomainException ORG-ERR-006 quando não pending
        invitation.Revoke(_clock.UtcNow);

        await _repository.SaveAsync(invitation, cancellationToken);
    }
}
