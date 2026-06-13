using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Services;
using NSubstitute.ExceptionExtensions;

namespace Authentication.Application.Tests.Services;

/// <summary>
/// Testes unitários para <see cref="SessionRevocationService"/>.
///
/// Cobre: idempotência de logout, evento auditável emitido apenas na primeira revogação,
/// captura de "já revogado" como sucesso.
///
/// Mapeia: TASK-07, design.md § 5.3, § 6.5, Req 9, Req 9.5, PBT-04.
/// </summary>
public sealed class SessionRevocationServiceTests
{
    private readonly IIdentityProvider _identityProvider = Substitute.For<IIdentityProvider>();
    private readonly IAuditEventEmitter _auditEmitter = Substitute.For<IAuditEventEmitter>();
    private readonly SessionRevocationService _sut;

    public SessionRevocationServiceTests()
    {
        _sut = new SessionRevocationService(_identityProvider, _auditEmitter);
    }

    [Fact(DisplayName = "Logout de sessão ativa revoga tokens e emite evento auditável")]
    public async Task ActiveSession_RevokesAndEmitsAuditEvent()
    {
        // Arrange
        var command = new LogoutCommand(UserId: Guid.NewGuid(), TenantId: Guid.NewGuid());

        _identityProvider
            .RevokeRefreshTokensAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.HandleAsync(command);

        // Assert
        await _identityProvider.Received(1)
            .RevokeRefreshTokensAsync(command.UserId, Arg.Any<CancellationToken>());
        await _auditEmitter.Received(1)
            .EmitAsync("session_revoked", command.TenantId, command.UserId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sessão já revogada: segunda chamada de logout retorna sucesso sem lançar exceção")]
    public async Task AlreadyRevokedSession_SecondLogout_ReturnsSuccess()
    {
        // Arrange
        var command = new LogoutCommand(UserId: Guid.NewGuid(), TenantId: Guid.NewGuid());

        // Primeira chamada bem-sucedida, segunda lança AlreadyRevokedException
        var callCount = 0;
        _identityProvider
            .RevokeRefreshTokensAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount > 1)
                    throw new IdentityProviderException("AUTH-ERR-004", "Sessão já revogada");
                return Task.CompletedTask;
            });

        // Act — duas chamadas consecutivas
        await _sut.HandleAsync(command);
        var act = () => _sut.HandleAsync(command);

        // Assert — não lança na segunda chamada
        await act.Should().NotThrowAsync("logout idempotente deve capturar 'já revogado' como sucesso (Req 9.5)");
    }

    [Fact(DisplayName = "Evento auditável emitido apenas na primeira revogação bem-sucedida")]
    public async Task AuditEvent_EmittedOnlyOnFirstRevocation()
    {
        // Arrange
        var command = new LogoutCommand(UserId: Guid.NewGuid(), TenantId: Guid.NewGuid());

        var callCount = 0;
        _identityProvider
            .RevokeRefreshTokensAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount > 1)
                    throw new IdentityProviderException("AUTH-ERR-004", "Sessão já revogada");
                return Task.CompletedTask;
            });

        // Act — duas chamadas
        await _sut.HandleAsync(command);
        await _sut.HandleAsync(command);

        // Assert — evento emitido somente na primeira chamada bem-sucedida
        await _auditEmitter.Received(1)
            .EmitAsync("session_revoked", command.TenantId, command.UserId, Arg.Any<CancellationToken>());
    }
}
