using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Ports.Results;
using Authentication.Application.Services;
using Authentication.Domain.ValueObjects;
using NSubstitute.ExceptionExtensions;

namespace Authentication.Application.Tests.Services;

/// <summary>
/// Testes unitários para <see cref="SessionTokenValidator"/>.
///
/// Mapeia: TASK-06, design.md § 5.3, Req 4, PBT-02.
/// </summary>
public sealed class SessionTokenValidatorTests
{
    private readonly IIdentityProvider _identityProvider = Substitute.For<IIdentityProvider>();
    private readonly SessionTokenValidator _sut;

    public SessionTokenValidatorTests()
    {
        _sut = new SessionTokenValidator(_identityProvider);
    }

    [Fact(DisplayName = "Token válido com tenant correto produz Session autenticada")]
    public async Task ValidToken_MatchingTenant_ProducesAuthenticatedSession()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        const string rawJwt = "valid.jwt.token";
        const string expectedFirebaseTenant = "firebase-tenant-abc";

        _identityProvider
            .VerifyTokenAsync(rawJwt, expectedFirebaseTenant, Arg.Any<CancellationToken>())
            .Returns(new VerifyTokenResult
            {
                ProviderUserRef = "uid-abc",
                FirebaseTenant = expectedFirebaseTenant,
                Email = "user@example.com",
                SignInProvider = "password",
            });

        // Act
        var result = await _sut.ValidateAsync(rawJwt, expectedFirebaseTenant, tenantId);

        // Assert
        result.session.State.Should().Be(SessionState.Authenticated);
        result.session.TenantId.Should().Be(tenantId);
        result.verifyResult.FirebaseTenant.Should().Be(expectedFirebaseTenant);
    }

    [Fact(DisplayName = "Token inválido lança IdentityProviderException com código de erro")]
    public async Task InvalidToken_ThrowsIdentityProviderException()
    {
        // Arrange
        const string rawJwt = "invalid.jwt";
        const string expectedFirebaseTenant = "firebase-tenant-abc";
        var tenantId = Guid.NewGuid();

        _identityProvider
            .VerifyTokenAsync(rawJwt, expectedFirebaseTenant, Arg.Any<CancellationToken>())
            .ThrowsAsync(new IdentityProviderException("AUTH-ERR-002", "Assinatura inválida"));

        // Act
        var act = () => _sut.ValidateAsync(rawJwt, expectedFirebaseTenant, tenantId);

        // Assert
        await act.Should().ThrowAsync<IdentityProviderException>()
            .Where(e => e.ErrorCode == "AUTH-ERR-002");
    }

    [Fact(DisplayName = "Token com firebase_tenant diferente do esperado lança IdentityProviderException AUTH-ERR-004")]
    public async Task TokenWithWrongTenant_ThrowsIdentityProviderException_WithCrossTenantCode()
    {
        // Arrange
        const string rawJwt = "valid.jwt.token";
        const string expectedTenant = "tenant-A";
        const string actualTenant = "tenant-B";
        var tenantId = Guid.NewGuid();

        _identityProvider
            .VerifyTokenAsync(rawJwt, expectedTenant, Arg.Any<CancellationToken>())
            .Returns(new VerifyTokenResult
            {
                ProviderUserRef = "uid-xyz",
                FirebaseTenant = actualTenant, // diferente do esperado
                Email = "user@example.com",
                SignInProvider = "password",
            });

        // Act
        var act = () => _sut.ValidateAsync(rawJwt, expectedTenant, tenantId);

        // Assert — TenantMatchSpec deve detectar a divergência
        await act.Should().ThrowAsync<IdentityProviderException>()
            .Where(e => e.ErrorCode == "AUTH-ERR-004");
    }

    [Fact(DisplayName = "Session produzida tem TenantId igual ao tenant resolvido pelo slug")]
    public async Task Session_TenantId_EqualsResolvedTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        const string rawJwt = "valid.jwt";
        const string firebaseTenant = "firebase-t1";

        _identityProvider
            .VerifyTokenAsync(rawJwt, firebaseTenant, Arg.Any<CancellationToken>())
            .Returns(new VerifyTokenResult
            {
                ProviderUserRef = "uid-1",
                FirebaseTenant = firebaseTenant,
                Email = "a@b.com",
                SignInProvider = "password",
            });

        // Act
        var (session, _) = await _sut.ValidateAsync(rawJwt, firebaseTenant, tenantId);

        // Assert
        session.TenantId.Should().Be(tenantId, "session deve estar escopada ao tenant resolvido (PBT-01)");
    }
}
