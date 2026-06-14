using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Organization.Application.Commands.Invitation;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Application.Tests.Handlers;

/// <summary>
/// Testes unitários para os handlers de convite de usuário.
/// </summary>
public sealed class InvitationHandlerTests
{
    private readonly IUserInvitationRepository _invitationRepo = Substitute.For<IUserInvitationRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IIdentityProvisioner _identityProvisioner = Substitute.For<IIdentityProvisioner>();
    private readonly IEventOutbox _outbox = Substitute.For<IEventOutbox>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ITokenHasher _tokenHasher = Substitute.For<ITokenHasher>();
    private readonly IOrganizationMetrics _metrics = Substitute.For<IOrganizationMetrics>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public InvitationHandlerTests()
    {
        _tenantContext.TenantId.Returns(_tenantId);
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());
        _clock.UtcNow.Returns(_now);
        _tokenHasher.GenerateToken().Returns(("token-claro", "hash-do-token"));
        _tokenHasher.Hash(Arg.Any<string>()).Returns(ci => $"hash-{ci.Arg<string>()}");
    }

    // ── InviteUserCommandHandler ──────────────────────────────────────────────

    [Fact]
    public async Task InviteUser_WithFreshEmail_CreatesInvitation()
    {
        // Arrange
        _invitationRepo.IsEmailActiveUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var handler = CreateInviteHandler();

        // Act
        var id = await handler.Handle(
            new InviteUserCommand(
                "novo@example.com",
                [(Guid.NewGuid(), "Vendedor")],
                _now.AddHours(72)),
            CancellationToken.None);

        // Assert
        id.Should().NotBe(Guid.Empty);
        await _invitationRepo.Received(1).SaveAsync(Arg.Any<UserInvitation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InviteUser_WithActiveEmail_ThrowsORG_ERR_003()
    {
        // Arrange
        _invitationRepo.IsEmailActiveUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = CreateInviteHandler();

        // Act
        var act = () => handler.Handle(
            new InviteUserCommand(
                "ativo@example.com",
                [(Guid.NewGuid(), "Vendedor")],
                _now.AddHours(72)),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ORG-ERR-003*");
    }

    [Fact]
    public async Task InviteUser_EnqueuesOutboxEvent()
    {
        // Arrange
        _invitationRepo.IsEmailActiveUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var handler = CreateInviteHandler();

        // Act
        await handler.Handle(
            new InviteUserCommand(
                "convidado@example.com",
                [(Guid.NewGuid(), "GestorBU")],
                _now.AddHours(48)),
            CancellationToken.None);

        // Assert
        await _outbox.Received().EnqueueAsync(
            Arg.Any<Organization.Domain.Events.IDomainEvent>(),
            _tenantId,
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    // ── RevokeInvitationCommandHandler ────────────────────────────────────────

    [Fact]
    public async Task RevokeInvitation_PendingInvitation_RevokesSuccessfully()
    {
        // Arrange
        var invitation = CreatePendingInvitation();
        _invitationRepo.GetByIdAsync(invitation.Id, Arg.Any<CancellationToken>()).Returns(invitation);
        var handler = CreateRevokeHandler();

        // Act
        await handler.Handle(new RevokeInvitationCommand(invitation.Id), CancellationToken.None);

        // Assert
        invitation.State.Should().Be(InvitationState.Revoked);
        await _invitationRepo.Received(1).SaveAsync(invitation, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeInvitation_NotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _invitationRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((UserInvitation?)null);
        var handler = CreateRevokeHandler();

        // Act
        var act = () => handler.Handle(new RevokeInvitationCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RevokeInvitation_AlreadyRevoked_ThrowsDomainException()
    {
        // Arrange
        var invitation = CreatePendingInvitation();
        invitation.Revoke(_now);
        _invitationRepo.GetByIdAsync(invitation.Id, Arg.Any<CancellationToken>()).Returns(invitation);
        var handler = CreateRevokeHandler();

        // Act
        var act = () => handler.Handle(new RevokeInvitationCommand(invitation.Id), CancellationToken.None);

        // Assert — domínio rejeita transição de estado terminal
        await act.Should().ThrowAsync<Domain.Exceptions.DomainException>();
    }

    // ── AcceptInvitationCommandHandler ────────────────────────────────────────

    [Fact]
    public async Task AcceptInvitation_ValidToken_CreatesUserWithMemberships()
    {
        // Arrange
        var buId = Guid.NewGuid();
        var invitation = CreatePendingInvitation(buId: buId);
        var tokenHash = $"hash-token-valido";

        _tokenHasher.Hash("token-valido").Returns(tokenHash);
        _invitationRepo.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>()).Returns(invitation);
        _identityProvisioner.ProvisionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("uid-123");

        var handler = CreateAcceptHandler();

        // Act
        var userId = await handler.Handle(
            new AcceptInvitationCommand("token-valido", "João Silva"),
            CancellationToken.None);

        // Assert
        userId.Should().NotBe(Guid.Empty);
        invitation.State.Should().Be(InvitationState.Accepted);
        await _userRepo.Received(1).SaveAsync(
            Arg.Is<User>(u => u.Memberships.Any(m => m.BuId == buId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptInvitation_InvalidToken_ThrowsORG_ERR_004()
    {
        // Arrange
        _tokenHasher.Hash(Arg.Any<string>()).Returns("hash-invalido");
        _invitationRepo.GetByTokenHashAsync("hash-invalido", Arg.Any<CancellationToken>())
            .Returns((UserInvitation?)null);
        var handler = CreateAcceptHandler();

        // Act
        var act = () => handler.Handle(
            new AcceptInvitationCommand("token-invalido", "Usuário"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ORG-ERR-004*");
    }

    [Fact]
    public async Task AcceptInvitation_ExpiredToken_ThrowsDomainException()
    {
        // Arrange
        var expiresAt = _now.AddHours(-1); // expirado
        var invitation = CreatePendingInvitation(expiresAt: expiresAt);
        var tokenHash = $"hash-token-expirado";
        _tokenHasher.Hash("token-expirado").Returns(tokenHash);
        _invitationRepo.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>()).Returns(invitation);
        var handler = CreateAcceptHandler();

        // Act
        var act = () => handler.Handle(
            new AcceptInvitationCommand("token-expirado", "Usuário"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Domain.Exceptions.DomainException>();
    }

    [Fact]
    public async Task AcceptInvitation_AlreadyAccepted_IsIdempotent()
    {
        // Arrange — o convite é criado com "hash-token-valido" (vide CreatePendingInvitation)
        // Para aceitar, o candidateHash deve ser "hash-token-valido"
        var tokenHash = "hash-token-valido";
        var invitation = CreatePendingInvitation();
        _tokenHasher.Hash("token-plain-para-aceitar").Returns(tokenHash);

        // Simula convite já aceito com o hash correto
        invitation.Accept(tokenHash, _now.AddMinutes(-5));
        _invitationRepo.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>()).Returns(invitation);

        var existingUser = User.Activate(invitation.Email, "João", "uid-existente", _tenantId, _now);
        _userRepo.GetByEmailAsync(invitation.Email, Arg.Any<CancellationToken>()).Returns(existingUser);

        var handler = CreateAcceptHandler();

        // Act — segundo aceite com mesmo token (convite já accepted)
        var userId = await handler.Handle(
            new AcceptInvitationCommand("token-plain-para-aceitar", "João Silva"),
            CancellationToken.None);

        // Assert — retorna userId existente sem criar duplicatas
        userId.Should().Be(existingUser.Id);
        await _identityProvisioner.DidNotReceive().ProvisionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptInvitation_IdentityProvisionerFails_InvitationRemainsInPending()
    {
        // Arrange — o hash do token deve coincidir com o armazenado no convite
        var tokenHash = "hash-token-valido"; // mesmo hash do CreatePendingInvitation
        _tokenHasher.Hash("token-valido-para-falha").Returns(tokenHash);
        var invitation = CreatePendingInvitation(); // usa InvitationToken.FromHash("hash-token-valido")
        _invitationRepo.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>()).Returns(invitation);
        _identityProvisioner.ProvisionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("IdP indisponível")); // NSubstitute.ExceptionExtensions

        var handler = CreateAcceptHandler();

        // Act
        var act = () => handler.Handle(
            new AcceptInvitationCommand("token-valido-para-falha", "Usuário"),
            CancellationToken.None);

        // Assert — falha no IdP deve propagar a exceção; o TransactionBehavior faz rollback
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _userRepo.DidNotReceive().SaveAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    // ── Helpers ──

    private UserInvitation CreatePendingInvitation(Guid? buId = null, DateTimeOffset? expiresAt = null)
    {
        var token = InvitationToken.FromHash("hash-token-valido");
        return UserInvitation.Create(
            "convidado@example.com",
            token,
            _tenantId,
            [(buId ?? Guid.NewGuid(), Role.Vendedor)],
            expiresAt ?? _now.AddHours(72),
            _now.AddMinutes(-10));
    }

    private InviteUserCommandHandler CreateInviteHandler()
        => new(_invitationRepo, _outbox, _tenantContext, _clock, _tokenHasher, _metrics);

    private RevokeInvitationCommandHandler CreateRevokeHandler()
        => new(_invitationRepo, _tenantContext, _clock);

    private AcceptInvitationCommandHandler CreateAcceptHandler()
        => new(_invitationRepo, _userRepo, _identityProvisioner, _outbox, _tenantContext, _clock, _tokenHasher);
}
