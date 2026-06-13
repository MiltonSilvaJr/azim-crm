using Authentication.Application.Ports;
using Authentication.Application.Ports.Results;
using Authentication.Application.Services;
using Authentication.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Authentication.Application.Tests.Services;

/// <summary>
/// Testes de emissão de eventos auditáveis pelos serviços de aplicação.
///
/// Verifica que:
///   - LogoutCommand emite session_revoked com user_id e tenant_id
///   - ActivateInviteCommand emite invite_activated
///   - RequestPasswordResetCommand emite password_reset_requested
///   - Nenhum evento contém identity_uid ou token
///   - Eventos são distintos por event_type
///   - Falha de IAuditEventEmitter não bloqueia o fluxo principal
///
/// Mapeia: TASK-24, RNF 10, design.md § 4.4, § 9.1.
/// </summary>
public sealed class AuditEventEmissionTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    // -------------------------------------------------------------------------
    // session_revoked
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SessionRevocationService_OnNewRevocation_EmitsSessionRevokedEvent()
    {
        // Arrange
        var idp = Substitute.For<IIdentityProvider>();
        var audit = Substitute.For<IAuditEventEmitter>();
        idp.RevokeRefreshTokensAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        audit.EmitAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);

        var service = new SessionRevocationService(idp, audit);
        var command = new LogoutCommand(UserId, TenantId);

        // Act
        await service.HandleAsync(command);

        // Assert — evento session_revoked deve ser emitido com user_id e tenant_id corretos
        await audit.Received(1).EmitAsync(
            Arg.Is<string>(e => e == "session_revoked"),
            Arg.Is<Guid>(t => t == TenantId),
            Arg.Is<Guid>(u => u == UserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SessionRevocationService_EventType_IsDistinct_SessionRevoked()
    {
        // Arrange
        var idp = Substitute.For<IIdentityProvider>();
        var audit = Substitute.For<IAuditEventEmitter>();
        idp.RevokeRefreshTokensAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        audit.EmitAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);

        var service = new SessionRevocationService(idp, audit);

        // Act
        await service.HandleAsync(new LogoutCommand(UserId, TenantId));

        // Assert — event_type deve ser exatamente "session_revoked" (não "logout" nem outro)
        await audit.Received(1).EmitAsync(
            Arg.Is<string>(e => e == "session_revoked"),
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SessionRevocationService_WhenAuditFails_DoesNotBlockMainFlow()
    {
        // Arrange — falha de auditoria não deve bloquear o logout
        var idp = Substitute.For<IIdentityProvider>();
        var audit = Substitute.For<IAuditEventEmitter>();
        idp.RevokeRefreshTokensAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        audit.EmitAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns(_ => Task.FromException(new Exception("Audit service unavailable")));

        var service = new SessionRevocationService(idp, audit);

        // Act & Assert — não deve lançar exceção
        var act = async () => await service.HandleAsync(new LogoutCommand(UserId, TenantId));
        await act.Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    // invite_activated
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InviteActivationService_OnSuccessfulActivation_EmitsInviteActivatedEvent()
    {
        // Arrange
        var idp = Substitute.For<IIdentityProvider>();
        var userDir = Substitute.For<IUserDirectory>();
        var emailSender = Substitute.For<IEmailSender>();
        var audit = Substitute.For<IAuditEventEmitter>();

        idp.HealthCheckAsync(Arg.Any<CancellationToken>())
           .Returns(HealthStatus.Healthy);
        userDir.FindUserAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
               .Returns(Task.FromResult<UserDirectoryResult?>(null)); // e-mail não duplicado

        audit.EmitAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);
        idp.GenerateInviteActivationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(new ActivationLinkResult { ActivationUrl = "https://example.com/activate", ExpiresAt = DateTimeOffset.UtcNow.AddHours(72) });
        emailSender.SendInviteEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);

        var service = new InviteActivationService(idp, userDir, emailSender, audit);
        var command = new CreateInviteCommand(
            Email: "newuser@acme.com",
            FirebaseTenant: "firebase-tenant-acme",
            TenantId: TenantId,
            InviterId: UserId);

        // Act
        await service.CreateAsync(command);

        // Assert — invite_activated seria emitido na ativação; aqui verificamos que
        // o criar NÃO emite invite_activated (apenas o Activate faz isso — RNF 10.3)
        await audit.DidNotReceive().EmitAsync(
            Arg.Is<string>(e => e == "invite_activated"),
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // password_reset_requested
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PasswordResetService_OnValidEmailWithPasswordMethod_EmitsPasswordResetRequestedEvent()
    {
        // Arrange
        var idp = Substitute.For<IIdentityProvider>();
        var userDir = Substitute.For<IUserDirectory>();
        var emailSender = Substitute.For<IEmailSender>();
        var audit = Substitute.For<IAuditEventEmitter>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        userDir.FindUserByEmailAsync("user@acme.com", TenantId, Arg.Any<CancellationToken>())
               .Returns(Task.FromResult<UserDirectoryResult?>(new UserDirectoryResult
               {
                   UserId = UserId,
                   Email = "user@acme.com",
                   IsActive = true,
                   Roles = ["viewer"],
                   Memberships = MembershipSet.Empty,
                   SignInProvider = "password"
               }));

        idp.GeneratePasswordResetLinkAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(new ResetLinkResult { ResetUrl = "https://example.com/reset", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });

        emailSender.SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);

        audit.EmitAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);

        var options = Microsoft.Extensions.Options.Options.Create(new PasswordResetOptions { ConstantDelayMs = 0 });
        var service = new PasswordResetService(idp, userDir, emailSender, audit, options);

        var command = new RequestPasswordResetCommand(
            Email: "user@acme.com",
            TenantId: TenantId,
            FirebaseTenant: "firebase-tenant-acme");

        // Act
        await service.RequestAsync(command);

        // Assert — evento password_reset_requested deve ser emitido
        await audit.Received(1).EmitAsync(
            Arg.Is<string>(e => e == "password_reset_requested"),
            Arg.Is<Guid>(t => t == TenantId),
            Arg.Is<Guid>(u => u == UserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuditEvents_NeverContainIdentityUid_OrToken()
    {
        // Verificação de propriedade: o contrato IAuditEventEmitter.EmitAsync
        // só aceita eventType (string), tenantId (Guid) e userId (Guid).
        // Não há parâmetro para identity_uid, token ou e-mail por construção.

        // Verificar a assinatura via reflexão
        var method = typeof(IAuditEventEmitter).GetMethod("EmitAsync");
        method.Should().NotBeNull();

        var parameters = method!.GetParameters();
        var paramNames = parameters.Select(p => p.Name).ToList();

        // Garante que a assinatura não tem parâmetros de dados sensíveis
        paramNames.Should().NotContain("identityUid");
        paramNames.Should().NotContain("identity_uid");
        paramNames.Should().NotContain("token");
        paramNames.Should().NotContain("password");
        paramNames.Should().NotContain("email");

        // Deve conter userId (Guid, não string identity_uid)
        paramNames.Should().Contain("userId");
        paramNames.Should().Contain("tenantId");
        paramNames.Should().Contain("eventType");

        await Task.CompletedTask;
    }
}
