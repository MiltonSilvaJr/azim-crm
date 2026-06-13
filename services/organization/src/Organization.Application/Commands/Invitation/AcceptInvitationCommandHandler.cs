using MediatR;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using DomainUser = Organization.Domain.Aggregates.User;

namespace Organization.Application.Commands.Invitation;

/// <summary>
/// Handler para <see cref="AcceptInvitationCommand"/>.
/// Valida token, estado e expiração; cria ou recupera identidade; cria usuário com memberships.
/// Idempotente: convite já aceito retorna o userId existente sem criar duplicatas (PBT-03).
/// Falha de <see cref="IIdentityProvisioner"/> faz rollback — convite permanece <c>Pending</c>.
/// </summary>
public sealed class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand, Guid>
{
    private readonly IUserInvitationRepository _invitationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProvisioner _identityProvisioner;
    private readonly IEventOutbox _outbox;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITokenHasher _tokenHasher;

    /// <summary>Inicializa o handler.</summary>
    public AcceptInvitationCommandHandler(
        IUserInvitationRepository invitationRepository,
        IUserRepository userRepository,
        IIdentityProvisioner identityProvisioner,
        IEventOutbox outbox,
        ITenantContext tenantContext,
        IClock clock,
        ITokenHasher tokenHasher)
    {
        _invitationRepository = invitationRepository;
        _userRepository = userRepository;
        _identityProvisioner = identityProvisioner;
        _outbox = outbox;
        _tenantContext = tenantContext;
        _clock = clock;
        _tokenHasher = tokenHasher;
    }

    /// <inheritdoc/>
    public async Task<Guid> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var tokenHash = _tokenHasher.Hash(request.PlainToken);

        // Recupera o convite pelo hash do token
        var invitation = await _invitationRepository.GetByTokenHashAsync(tokenHash, cancellationToken)
            ?? throw new InvalidOperationException("Convite inválido ou expirado. ORG-ERR-004");

        // Idempotência: convite já aceito — retorna userId sem duplicar (PBT-03)
        if (invitation.State == Domain.Aggregates.InvitationState.Accepted)
        {
            var existingUser = await _userRepository.GetByEmailAsync(invitation.Email, cancellationToken);
            if (existingUser is not null)
                return existingUser.Id;

            // Edge case: aceite persistido mas usuário ainda não (janela entre saves)
            throw new InvalidOperationException("Convite já utilizado. ORG-ERR-005");
        }

        // Delega ao domínio: valida hash, expiração e estado (ORG-ERR-004, ORG-ERR-006)
        invitation.Accept(tokenHash, _clock.UtcNow);

        // Provisiona identidade (idempotente por e-mail no IdP)
        var identityUid = await _identityProvisioner.ProvisionAsync(
            invitation.Email,
            request.DisplayName,
            cancellationToken);

        // Cria usuário
        var user = DomainUser.Activate(
            invitation.Email,
            request.DisplayName,
            identityUid,
            tenantId,
            _clock.UtcNow);

        // Atribui memberships conforme o convite
        foreach (var (buId, role) in invitation.TargetMemberships)
        {
            user.AssignMembership(buId, role, Guid.NewGuid());
        }

        // Publica eventos (UserActivated) via Outbox
        foreach (var domainEvent in user.DomainEvents)
        {
            await _outbox.EnqueueAsync(domainEvent, tenantId, _tenantContext.CorrelationId, cancellationToken);
        }

        user.ClearDomainEvents();

        await _userRepository.SaveAsync(user, cancellationToken);
        await _invitationRepository.SaveAsync(invitation, cancellationToken);

        return user.Id;
    }
}
