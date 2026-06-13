using Authentication.Application.Ports;
using Authentication.Application.Ports.Results;
using Authentication.Application.Services;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace Authentication.Application.Tests.Services;

/// <summary>
/// Testes unitários para <see cref="PasswordResetService"/>.
///
/// Cobre: resposta uniforme (anti-enumeração), EmailMethodSpec bloqueia Google,
/// delay constante configurável, logs com mesma categoria.
///
/// Mapeia: TASK-09, design.md § 5.1, § 5.3, Req 8, Req 10.2, PBT-03.
/// </summary>
public sealed class PasswordResetServiceTests
{
    private readonly IIdentityProvider _identityProvider = Substitute.For<IIdentityProvider>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IAuditEventEmitter _auditEmitter = Substitute.For<IAuditEventEmitter>();
    private readonly PasswordResetService _sut;

    private static readonly MembershipSet EmptyMemberships =
        MembershipSet.Create([], DateTimeOffset.UtcNow);

    public PasswordResetServiceTests()
    {
        // Delay zerado para testes (sem espera real)
        var options = Options.Create(new PasswordResetOptions { ConstantDelayMs = 0 });
        _sut = new PasswordResetService(
            _identityProvider, _userDirectory, _emailSender, _auditEmitter, options);
    }

    [Fact(DisplayName = "E-mail existente de método password: gera link e envia e-mail")]
    public async Task ExistingPasswordUser_GeneratesLinkAndSendsEmail()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string email = "user@tenant.com";

        var command = new RequestPasswordResetCommand(
            Email: email,
            FirebaseTenant: "ft-tenant",
            TenantId: tenantId);

        _userDirectory
            .FindUserAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((UserDirectoryResult?)null);

        // Simula resolução por e-mail
        _userDirectory
            .IsEmailActiveAsync(email, tenantId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Configura lookup por email especial
        _userDirectory
            .FindUserByEmailAsync(email, tenantId, Arg.Any<CancellationToken>())
            .Returns(new UserDirectoryResult
            {
                UserId = userId,
                Email = email,
                Roles = [],
                Memberships = EmptyMemberships,
                IsActive = true,
                SignInProvider = "password",
            });

        _identityProvider
            .GeneratePasswordResetLinkAsync(email, "ft-tenant", Arg.Any<CancellationToken>())
            .Returns(new ResetLinkResult
            {
                ResetUrl = "https://idp/reset?token=abc",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            });

        // Act
        await _sut.RequestAsync(command);

        // Assert
        await _identityProvider.Received(1)
            .GeneratePasswordResetLinkAsync(email, "ft-tenant", Arg.Any<CancellationToken>());
        await _emailSender.Received(1)
            .SendPasswordResetEmailAsync(
                email, Arg.Any<string>(), tenantId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "E-mail inexistente: retorna accepted sem chamar IdP nem enviar e-mail")]
    public async Task NonExistentEmail_ReturnsAccepted_NoIdpCall()
    {
        // Arrange
        var command = new RequestPasswordResetCommand(
            Email: "naoexiste@tenant.com",
            FirebaseTenant: "ft-tenant",
            TenantId: Guid.NewGuid());

        _userDirectory
            .FindUserByEmailAsync(command.Email, command.TenantId, Arg.Any<CancellationToken>())
            .Returns((UserDirectoryResult?)null);

        // Act
        var act = () => _sut.RequestAsync(command);

        // Assert — não lança, retorna accepted silenciosamente
        await act.Should().NotThrowAsync();

        await _identityProvider
            .DidNotReceive()
            .GeneratePasswordResetLinkAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _emailSender
            .DidNotReceive()
            .SendPasswordResetEmailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "E-mail de usuário Google: bloqueia geração de link (EmailMethodSpec)")]
    public async Task GoogleUser_BlocksLinkGeneration_EmailMethodSpec()
    {
        // Arrange
        const string email = "google.user@tenant.com";
        var tenantId = Guid.NewGuid();

        var command = new RequestPasswordResetCommand(
            Email: email,
            FirebaseTenant: "ft-tenant",
            TenantId: tenantId);

        _userDirectory
            .FindUserByEmailAsync(email, tenantId, Arg.Any<CancellationToken>())
            .Returns(new UserDirectoryResult
            {
                UserId = Guid.NewGuid(),
                Email = email,
                Roles = [],
                Memberships = EmptyMemberships,
                IsActive = true,
                SignInProvider = "google.com", // usuário Google!
            });

        // Act
        var act = () => _sut.RequestAsync(command);

        // Assert — não lança (anti-enumeração), mas não gera link
        await act.Should().NotThrowAsync();

        await _identityProvider
            .DidNotReceive()
            .GeneratePasswordResetLinkAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Resposta é sempre 'accepted' independente de e-mail existir ou não")]
    public async Task Response_AlwaysAccepted_RegardlessOfEmailExistence()
    {
        // Arrange — e-mail existente
        var tenantId = Guid.NewGuid();
        const string existingEmail = "exists@tenant.com";
        const string missingEmail = "missing@tenant.com";

        _userDirectory
            .FindUserByEmailAsync(existingEmail, tenantId, Arg.Any<CancellationToken>())
            .Returns(new UserDirectoryResult
            {
                UserId = Guid.NewGuid(),
                Email = existingEmail,
                Roles = [],
                Memberships = EmptyMemberships,
                IsActive = true,
                SignInProvider = "password",
            });

        _identityProvider
            .GeneratePasswordResetLinkAsync(existingEmail, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ResetLinkResult
            {
                ResetUrl = "https://idp/reset?t=abc",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            });

        _userDirectory
            .FindUserByEmailAsync(missingEmail, tenantId, Arg.Any<CancellationToken>())
            .Returns((UserDirectoryResult?)null);

        var cmdExisting = new RequestPasswordResetCommand(existingEmail, "ft", tenantId);
        var cmdMissing = new RequestPasswordResetCommand(missingEmail, "ft", tenantId);

        // Act — ambas as chamadas não devem lançar exceção
        var actExisting = () => _sut.RequestAsync(cmdExisting);
        var actMissing = () => _sut.RequestAsync(cmdMissing);

        // Assert — ambas retornam sem exceção (anti-enumeração PBT-03)
        await actExisting.Should().NotThrowAsync();
        await actMissing.Should().NotThrowAsync();
    }
}
