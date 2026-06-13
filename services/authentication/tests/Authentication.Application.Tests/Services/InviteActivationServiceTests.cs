using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Ports.Results;
using Authentication.Application.Services;
using Authentication.Domain.ValueObjects;
using NSubstitute.ExceptionExtensions;

namespace Authentication.Application.Tests.Services;

/// <summary>
/// Testes unitários para <see cref="InviteActivationService"/>.
///
/// Cobre: criação de convite, ativação, uso único + expiração enforçados,
/// falha de e-mail não reverte convite (AUTH-ERR-032), e-mail duplicado (AUTH-ERR-030),
/// link expirado/consumido → 410 (AUTH-ERR-033).
///
/// Mapeia: TASK-08, design.md § 5.1, § 5.3, Req 7, DD-009.
/// </summary>
public sealed class InviteActivationServiceTests
{
    private readonly IIdentityProvider _identityProvider = Substitute.For<IIdentityProvider>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IAuditEventEmitter _auditEmitter = Substitute.For<IAuditEventEmitter>();
    private readonly InviteActivationService _sut;

    public InviteActivationServiceTests()
    {
        _sut = new InviteActivationService(
            _identityProvider, _userDirectory, _emailSender, _auditEmitter);
    }

    // =========================================================================
    // Create
    // =========================================================================

    [Fact(DisplayName = "Create: convite criado com sucesso → retorna ActivationLinkResult e dispara e-mail")]
    public async Task Create_ValidInvite_ReturnsLinkAndSendsEmail()
    {
        // Arrange
        var command = new CreateInviteCommand(
            Email: "novo@tenant.com",
            FirebaseTenant: "ft-tenant",
            TenantId: Guid.NewGuid(),
            InviterId: Guid.NewGuid());

        var expectedLink = new ActivationLinkResult
        {
            ActivationUrl = "https://idp.example.com/activate?token=abc",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(72),
        };

        _userDirectory
            .IsEmailActiveAsync(command.Email, command.TenantId, Arg.Any<CancellationToken>())
            .Returns(false);

        _identityProvider
            .GenerateInviteActivationAsync(command.Email, command.FirebaseTenant, Arg.Any<CancellationToken>())
            .Returns(expectedLink);

        // Act
        var result = await _sut.CreateAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.ActivationUrl.Should().Be(expectedLink.ActivationUrl);

        await _emailSender.Received(1)
            .SendInviteEmailAsync(
                command.Email,
                expectedLink.ActivationUrl,
                command.TenantId,
                Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Create: e-mail já ativo no tenant → lança IdentityProviderException AUTH-ERR-030")]
    public async Task Create_DuplicateEmail_ThrowsException_Auth030()
    {
        // Arrange
        var command = new CreateInviteCommand(
            Email: "existente@tenant.com",
            FirebaseTenant: "ft-tenant",
            TenantId: Guid.NewGuid(),
            InviterId: Guid.NewGuid());

        _userDirectory
            .IsEmailActiveAsync(command.Email, command.TenantId, Arg.Any<CancellationToken>())
            .Returns(true); // e-mail já ativo

        // Act
        var act = () => _sut.CreateAsync(command);

        // Assert
        await act.Should().ThrowAsync<IdentityProviderException>()
            .Where(e => e.ErrorCode == "AUTH-ERR-030");

        // Não deve chamar IdP nem enviar e-mail
        await _identityProvider
            .DidNotReceive()
            .GenerateInviteActivationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Create: falha de e-mail → retorna link criado (convite não revertido) com AUTH-ERR-032")]
    public async Task Create_EmailFailure_ReturnsLink_DoesNotRevert()
    {
        // Arrange
        var command = new CreateInviteCommand(
            Email: "convidado@tenant.com",
            FirebaseTenant: "ft-tenant",
            TenantId: Guid.NewGuid(),
            InviterId: Guid.NewGuid());

        var expectedLink = new ActivationLinkResult
        {
            ActivationUrl = "https://idp.example.com/activate?token=xyz",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(72),
        };

        _userDirectory
            .IsEmailActiveAsync(command.Email, command.TenantId, Arg.Any<CancellationToken>())
            .Returns(false);

        _identityProvider
            .GenerateInviteActivationAsync(command.Email, command.FirebaseTenant, Arg.Any<CancellationToken>())
            .Returns(expectedLink);

        _emailSender
            .SendInviteEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EmailSenderException("Serviço de e-mail indisponível"));

        // Act — falha de e-mail lança com código específico
        var act = () => _sut.CreateAsync(command);

        // Assert — lança AUTH-ERR-032 (convite criado, e-mail não enviado)
        await act.Should().ThrowAsync<InviteEmailFailedException>()
            .Where(e => e.ErrorCode == "AUTH-ERR-032" && e.ActivationLink != null);

        // IdP foi chamado (convite foi criado no IdP)
        await _identityProvider
            .Received(1)
            .GenerateInviteActivationAsync(command.Email, command.FirebaseTenant, Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Activate
    // =========================================================================

    [Fact(DisplayName = "Activate: link issued e não expirado → ativa conta e emite evento auditável")]
    public async Task Activate_ValidLink_ActivatesAndEmitsAudit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var command = new ActivateInviteCommand(
            ActivationToken: "token-issued",
            State: InviteLinkState.Issued,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(48),
            UserId: userId,
            TenantId: tenantId);

        // Act
        await _sut.ActivateAsync(command);

        // Assert
        await _auditEmitter.Received(1)
            .EmitAsync("invite_activated", tenantId, userId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Activate: link expired → lança IdentityProviderException AUTH-ERR-033")]
    public async Task Activate_ExpiredLink_ThrowsException_Auth033()
    {
        // Arrange
        var command = new ActivateInviteCommand(
            ActivationToken: "token-expired",
            State: InviteLinkState.Expired,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(-1), // já expirado
            UserId: Guid.NewGuid(),
            TenantId: Guid.NewGuid());

        // Act
        var act = () => _sut.ActivateAsync(command);

        // Assert
        await act.Should().ThrowAsync<IdentityProviderException>()
            .Where(e => e.ErrorCode == "AUTH-ERR-033");
    }

    [Fact(DisplayName = "Activate: link consumed → lança IdentityProviderException AUTH-ERR-033")]
    public async Task Activate_ConsumedLink_ThrowsException_Auth033()
    {
        // Arrange
        var command = new ActivateInviteCommand(
            ActivationToken: "token-consumed",
            State: InviteLinkState.Consumed,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(24),
            UserId: Guid.NewGuid(),
            TenantId: Guid.NewGuid());

        // Act
        var act = () => _sut.ActivateAsync(command);

        // Assert
        await act.Should().ThrowAsync<IdentityProviderException>()
            .Where(e => e.ErrorCode == "AUTH-ERR-033");
    }

    [Fact(DisplayName = "Activate: link issued mas prazo atingido → lança IdentityProviderException AUTH-ERR-033")]
    public async Task Activate_IssuedButExpiredByTime_ThrowsException_Auth033()
    {
        // Arrange
        var command = new ActivateInviteCommand(
            ActivationToken: "token-issued-expired",
            State: InviteLinkState.Issued, // estado ainda Issued...
            ExpiresAt: DateTimeOffset.UtcNow.AddSeconds(-1), // ...mas prazo atingido
            UserId: Guid.NewGuid(),
            TenantId: Guid.NewGuid());

        // Act
        var act = () => _sut.ActivateAsync(command);

        // Assert
        await act.Should().ThrowAsync<IdentityProviderException>()
            .Where(e => e.ErrorCode == "AUTH-ERR-033");
    }
}
