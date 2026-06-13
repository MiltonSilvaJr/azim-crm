using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Infrastructure.Jwks;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Xunit;

namespace Authentication.Infrastructure.Tests.Jwks;

/// <summary>
/// Testes de <see cref="JwksTokenVerifier"/> com chaves RSA geradas em memória.
///
/// Estratégia: cria RSA key pair local, gera JWT assinado e verifica localmente
/// sem nenhuma dependência de rede — simula o comportamento de JWKS em memória.
///
/// Mapeia: Req 4.5; RNF 2, RNF 6.2; design.md § 6.2, § 6.4; DD-002, TASK-11.
/// </summary>
public sealed class JwksTokenVerifierTests : IDisposable
{
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly string _issuer = "https://securetoken.google.com/test-project";
    private readonly string _audience = "test-project";
    private readonly string _kid = "test-key-id";

    public JwksTokenVerifierTests()
    {
        _rsa = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(_rsa) { KeyId = _kid };
    }

    public void Dispose() => _rsa.Dispose();

    // =========================================================================
    // Contrato
    // =========================================================================

    [Fact(DisplayName = "JwksTokenVerifier deve implementar ITokenVerifier (TASK-11)")]
    public void JwksTokenVerifier_ShouldImplement_ITokenVerifier()
    {
        typeof(JwksTokenVerifier)
            .Should()
            .Implement<ITokenVerifier>(
                because: "JwksTokenVerifier é o adapter concreto de ITokenVerifier (DD-002, TASK-11)");
    }

    // =========================================================================
    // Token com assinatura válida passa
    // =========================================================================

    [Fact(DisplayName = "VerifySignatureAsync — token com assinatura válida não lança exceção (TASK-11, Req 4.5)")]
    public async Task VerifySignatureAsync_ValidSignature_ReturnsResult()
    {
        // Arrange
        var jwt = CreateSignedJwt(sub: "uid-123", email: "test@acme.com");
        var verifier = CreateVerifier();

        // Act
        Func<Task> act = async () => await verifier.VerifySignatureAsync(jwt);

        // Assert — token válido não deve lançar exceção
        await act.Should().NotThrowAsync(
            because: "token assinado com chave correta deve ser aceito (Req 4.5, DD-002)");
    }

    // =========================================================================
    // Assinatura inválida — lança IdentityProviderException
    // =========================================================================

    [Fact(DisplayName = "VerifySignatureAsync — token com assinatura inválida lança IdentityProviderException AUTH-ERR-002 (TASK-11)")]
    public async Task VerifySignatureAsync_InvalidSignature_ThrowsIdentityProviderException()
    {
        // Arrange — cria JWT com outra chave (assinatura inválida para o verifier)
        using var otherRsa = RSA.Create(2048);
        var otherKey = new RsaSecurityKey(otherRsa) { KeyId = _kid };
        var jwt = CreateJwtWithKey(otherKey, sub: "uid-tampered");
        var verifier = CreateVerifier();

        // Act
        Func<Task> act = async () => await verifier.VerifySignatureAsync(jwt);

        // Assert
        var ex = await act.Should().ThrowAsync<IdentityProviderException>(
            because: "token com assinatura inválida deve lançar IdentityProviderException (Req 4.5)");

        ex.Which.ErrorCode.Should().Be("AUTH-ERR-002",
            because: "assinatura inválida deve produzir AUTH-ERR-002 (design.md § 12)");
    }

    // =========================================================================
    // Token expirado — lança IdentityProviderException
    // =========================================================================

    [Fact(DisplayName = "VerifySignatureAsync — token expirado lança IdentityProviderException AUTH-ERR-003 (TASK-11)")]
    public async Task VerifySignatureAsync_ExpiredToken_ThrowsIdentityProviderException()
    {
        // Arrange — cria JWT que expirou no passado
        var jwt = CreateSignedJwt(sub: "uid-exp", expiration: DateTimeOffset.UtcNow.AddMinutes(-5));
        var verifier = CreateVerifier();

        // Act
        Func<Task> act = async () => await verifier.VerifySignatureAsync(jwt);

        // Assert
        var ex = await act.Should().ThrowAsync<IdentityProviderException>(
            because: "token expirado deve lançar IdentityProviderException (Req 4.5)");

        ex.Which.ErrorCode.Should().Be("AUTH-ERR-003",
            because: "token expirado deve produzir AUTH-ERR-003 (design.md § 12)");
    }

    // =========================================================================
    // Cache reduz chamadas
    // =========================================================================

    [Fact(DisplayName = "VerifySignatureAsync — segunda chamada usa chave em cache (TASK-11, RNF 2)")]
    public async Task VerifySignatureAsync_SecondCall_UsesCache()
    {
        // Arrange
        var callCount = 0;
        var verifier = CreateVerifier(onKeyLoad: () => callCount++);
        var jwt = CreateSignedJwt(sub: "uid-cache");

        // Act
        await verifier.VerifySignatureAsync(jwt);
        await verifier.VerifySignatureAsync(jwt);

        // Assert
        callCount.Should().Be(1,
            because: "a segunda verificação deve usar as chaves em cache " +
                     "sem nova carga do endpoint JWKS (RNF 2)");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private string CreateSignedJwt(
        string sub,
        string? email = null,
        DateTimeOffset? expiration = null)
    {
        return CreateJwtWithKey(_signingKey, sub, email, expiration);
    }

    private string CreateJwtWithKey(
        RsaSecurityKey key,
        string sub,
        string? email = null,
        DateTimeOffset? expiration = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, sub),
            new(JwtRegisteredClaimNames.Email, email ?? $"{sub}@test.com"),
        };

        var expiresAt = expiration ?? DateTimeOffset.UtcNow.AddHours(1);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private JwksTokenVerifier CreateVerifier(Action? onKeyLoad = null)
    {
        var publicRsa = RSA.Create();
        publicRsa.ImportRSAPublicKey(_rsa.ExportRSAPublicKey(), out _);
        var publicKey = new RsaSecurityKey(publicRsa) { KeyId = _kid };

        return JwksTokenVerifier.CreateWithInMemoryKeys(
            validationKey: publicKey,
            issuer: _issuer,
            audience: _audience,
            onKeyLoad: onKeyLoad);
    }
}
