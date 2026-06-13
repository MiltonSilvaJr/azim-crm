using MediatR;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;

namespace Organization.Application.Commands.Invitation;

/// <summary>
/// Handler para <see cref="InviteUserCommand"/>.
/// Cria o convite com token hash e enfileira o e-mail via Outbox na mesma transação (DD-004).
/// Bloqueia e-mail de usuário ativo (ORG-ERR-003) sem revelar existência da conta.
/// </summary>
public sealed class InviteUserCommandHandler : IRequestHandler<InviteUserCommand, Guid>
{
    private readonly IUserInvitationRepository _invitationRepository;
    private readonly IEventOutbox _outbox;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITokenHasher _tokenHasher;

    /// <summary>Inicializa o handler.</summary>
    public InviteUserCommandHandler(
        IUserInvitationRepository invitationRepository,
        IEventOutbox outbox,
        ITenantContext tenantContext,
        IClock clock,
        ITokenHasher tokenHasher)
    {
        _invitationRepository = invitationRepository;
        _outbox = outbox;
        _tenantContext = tenantContext;
        _clock = clock;
        _tokenHasher = tokenHasher;
    }

    /// <inheritdoc/>
    public async Task<Guid> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        // Bloqueia e-mail de usuário ativo — sem revelar existência (ORG-ERR-003)
        var emailIsActive = await _invitationRepository.IsEmailActiveUserAsync(request.Email, cancellationToken);
        if (emailIsActive)
            throw new InvalidOperationException("Não foi possível concluir o convite. ORG-ERR-003");

        // Gera token criptográfico e armazena apenas o hash
        var (_, tokenHash) = _tokenHasher.GenerateToken();
        var token = InvitationToken.FromHash(tokenHash);

        // Constrói os target memberships
        var targetMemberships = request.TargetMemberships
            .Select(m => (m.BuId, Role.Create(m.Role)))
            .ToList();

        var invitation = UserInvitation.Create(
            request.Email,
            token,
            tenantId,
            targetMemberships,
            request.ExpiresAt,
            _clock.UtcNow);

        // Enfileira domain events (UserInvited) via Outbox — mesma transação (DD-004)
        foreach (var domainEvent in invitation.DomainEvents)
        {
            await _outbox.EnqueueAsync(domainEvent, tenantId, _tenantContext.CorrelationId, cancellationToken);
        }

        invitation.ClearDomainEvents();
        await _invitationRepository.SaveAsync(invitation, cancellationToken);

        return invitation.Id;
    }
}
