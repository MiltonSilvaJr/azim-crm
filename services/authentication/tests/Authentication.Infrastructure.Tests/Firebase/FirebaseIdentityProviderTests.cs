using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Ports.Results;
using Authentication.Infrastructure.Firebase;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Authentication.Infrastructure.Tests.Firebase;

/// <summary>
/// Testes de integração de <see cref="FirebaseIdentityProvider"/> contra stubs de IFirebaseTokenVerifier.
///
/// Mapeia: Req 6, Req 6.5; RNF 9; design.md § 6.4; DD-001, DD-005; RISK-AUTH-01, TASK-10.
///
/// Estratégia: o FirebaseIdentityProvider é construído com um IFirebaseTokenVerifier stub
/// que permite simular respostas do SDK sem emulador real, isolando totalmente
/// a dependência do Firebase.
/// </summary>
public sealed class FirebaseIdentityProviderTests
{
    // =========================================================================
    // Contrato — FirebaseIdentityProvider implementa IIdentityProvider
    // =========================================================================

    [Fact(DisplayName = "FirebaseIdentityProvider deve implementar IIdentityProvider (TASK-10, Req 6)")]
    public void FirebaseIdentityProvider_ShouldImplement_IIdentityProvider()
    {
        typeof(FirebaseIdentityProvider)
            .Should()
            .Implement<IIdentityProvider>(
                because: "FirebaseIdentityProvider é o adapter concreto de IIdentityProvider (DD-001, Req 6)");
    }

    // =========================================================================
    // Token válido retorna VerifyTokenResult
    // =========================================================================

    [Fact(DisplayName = "VerifyTokenAsync — token válido retorna VerifyTokenResult sem identity_uid exposto (TASK-10, Req 6)")]
    public async Task VerifyTokenAsync_ValidToken_ReturnsVerifyTokenResult()
    {
        // Arrange
        var stub = Substitute.For<IFirebaseTokenVerifier>();
        stub.VerifyIdTokenAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new FirebaseTokenResult(
                Uid: "firebase-uid-abc123",
                FirebaseTenant: "tenant-001",
                Email: "user@acme.com",
                SignInProvider: "password"));

        var provider = new FirebaseIdentityProvider(stub);

        // Act
        var result = await provider.VerifyTokenAsync("raw.jwt.token", "tenant-001");

        // Assert
        result.Should().NotBeNull(because: "token válido deve produzir resultado não-nulo");
        result.ProviderUserRef.Should().NotBeNullOrEmpty(
            because: "referência opaca ao usuário deve ser retornada");
        result.FirebaseTenant.Should().Be("tenant-001");
        result.Email.Should().Be("user@acme.com");
        result.SignInProvider.Should().Be("password");
    }

    // =========================================================================
    // Exceções do SDK mapeadas para IdentityProviderException
    // =========================================================================

    [Fact(DisplayName = "VerifyTokenAsync — FirebaseAdapterException mapeia para IdentityProviderException (TASK-10, Req 6.5)")]
    public async Task VerifyTokenAsync_WhenFirebaseAdapterExceptionThrown_MapsTo_IdentityProviderException()
    {
        // Arrange
        var stub = Substitute.For<IFirebaseTokenVerifier>();
        stub.VerifyIdTokenAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new FirebaseAdapterException("TOKEN_EXPIRED", "Token expirado pelo SDK"));

        var provider = new FirebaseIdentityProvider(stub);

        // Act
        Func<Task> act = async () => await provider.VerifyTokenAsync("expired.jwt", "tenant-001");

        // Assert
        var ex = await act.Should().ThrowAsync<IdentityProviderException>(
            because: "qualquer exceção do Firebase SDK deve ser mapeada para IdentityProviderException " +
                     "(Req 6.5) — nunca propagar tipo do SDK ao chamador");

        ex.Which.ErrorCode.Should().NotBeNullOrEmpty(
            because: "IdentityProviderException deve carregar código do catálogo de erros");
    }

    [Fact(DisplayName = "VerifyTokenAsync — exceção genérica mapeia para AUTH-ERR-090 (TASK-10, Req 6.5)")]
    public async Task VerifyTokenAsync_WhenUnexpectedSdkException_MapsToInternalErrorCode()
    {
        // Arrange
        var stub = Substitute.For<IFirebaseTokenVerifier>();
        stub.VerifyIdTokenAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected SDK error"));

        var provider = new FirebaseIdentityProvider(stub);

        // Act
        Func<Task> act = async () => await provider.VerifyTokenAsync("some.jwt", "tenant-001");

        // Assert
        var ex = await act.Should().ThrowAsync<IdentityProviderException>(
            because: "exceção inesperada deve ser mapeada para AUTH-ERR-090 (Req 6.5)");

        ex.Which.ErrorCode.Should().Be("AUTH-ERR-090");
    }

    // =========================================================================
    // Circuit breaker — abre após N falhas consecutivas
    // =========================================================================

    [Fact(DisplayName = "VerifyTokenAsync — circuit breaker abre após N falhas e retorna AUTH-ERR-020 (TASK-10, RNF 9)")]
    public async Task VerifyTokenAsync_AfterConsecutiveFailures_CircuitBreakerOpens()
    {
        // Arrange — stub que sempre falha com exceção do tipo "serviço indisponível"
        var stub = Substitute.For<IFirebaseTokenVerifier>();
        stub.VerifyIdTokenAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new FirebaseAdapterException("SERVICE_UNAVAILABLE", "Firebase indisponível"));

        // Cria provider com circuit breaker de baixo threshold para o teste
        var provider = FirebaseIdentityProvider.CreateForTesting(stub, circuitBreakerThreshold: 3);

        // Act — dispara N falhas para acumular no circuit breaker
        for (var i = 0; i < 3; i++)
        {
            try { await provider.VerifyTokenAsync("jwt", "tenant"); }
            catch { /* ignora para acumular falhas */ }
        }

        // Aguarda um ciclo para o circuit breaker consolidar o estado
        await Task.Delay(100);

        // A próxima chamada deve atingir o circuit breaker aberto
        Func<Task> act = async () => await provider.VerifyTokenAsync("jwt", "tenant");

        // Assert
        var ex = await act.Should().ThrowAsync<IdentityProviderException>(
            because: "circuit breaker aberto deve retornar IdentityProviderException (RNF 9.2)");

        ex.Which.ErrorCode.Should().Be("AUTH-ERR-020",
            because: "circuit breaker aberto deve sinalizar indisponibilidade com AUTH-ERR-020");
    }

    // =========================================================================
    // RevokeRefreshTokensAsync
    // =========================================================================

    [Fact(DisplayName = "RevokeRefreshTokensAsync — chamada bem-sucedida não lança exceção (TASK-10, Req 9.5)")]
    public async Task RevokeRefreshTokensAsync_Success_DoesNotThrow()
    {
        // Arrange
        var stub = Substitute.For<IFirebaseTokenVerifier>();
        stub.RevokeRefreshTokensAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var provider = new FirebaseIdentityProvider(stub);
        var userId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await provider.RevokeRefreshTokensAsync(userId);

        // Assert
        await act.Should().NotThrowAsync(
            because: "revogação bem-sucedida não deve lançar exceção");
    }

    // =========================================================================
    // HealthCheckAsync
    // =========================================================================

    [Fact(DisplayName = "HealthCheckAsync — retorna Unhealthy quando SDK falha (TASK-10, RNF 3.2)")]
    public async Task HealthCheckAsync_WhenSdkFails_ReturnsUnhealthy()
    {
        // Arrange
        var stub = Substitute.For<IFirebaseTokenVerifier>();
        stub.HealthCheckAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("SDK indisponível"));

        var provider = new FirebaseIdentityProvider(stub);

        // Act
        var status = await provider.HealthCheckAsync();

        // Assert
        status.Should().Be(HealthStatus.Unhealthy,
            because: "qualquer falha no health check do IdP deve retornar Unhealthy sem lançar exceção (RNF 3.2)");
    }

    [Fact(DisplayName = "HealthCheckAsync — retorna Healthy quando SDK responde (TASK-10, RNF 3.2)")]
    public async Task HealthCheckAsync_WhenSdkResponds_ReturnsHealthy()
    {
        // Arrange
        var stub = Substitute.For<IFirebaseTokenVerifier>();
        stub.HealthCheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var provider = new FirebaseIdentityProvider(stub);

        // Act
        var status = await provider.HealthCheckAsync();

        // Assert
        status.Should().Be(HealthStatus.Healthy,
            because: "SDK disponível deve retornar Healthy");
    }
}
