using Authentication.Application.Ports;
using Authentication.Infrastructure.HealthChecks;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PortHealthStatus = Authentication.Application.Ports.Results.HealthStatus;

namespace Authentication.Infrastructure.Tests.HealthChecks;

/// <summary>
/// Testes do <see cref="IdentityProviderHealthCheck"/>.
///
/// Verifica que:
///   - IdP disponível → HealthCheckResult.Healthy
///   - IdP indisponível → HealthCheckResult.Unhealthy (sem expor detalhe interno)
///   - Exceção inesperada → HealthCheckResult.Unhealthy (sem propagar exceção)
///   - IdP degradado → HealthCheckResult.Degraded
///
/// Mapeia: TASK-21, RNF 3.2, design.md § 11.
/// </summary>
public sealed class IdentityProviderHealthCheckTests
{
    private readonly IIdentityProvider _idp = Substitute.For<IIdentityProvider>();

    private HealthCheckContext BuildContext() =>
        new()
        {
            Registration = new HealthCheckRegistration(
                "identity_provider",
                new IdentityProviderHealthCheck(_idp),
                HealthStatus.Unhealthy,
                ["readiness"])
        };

    [Fact]
    public async Task CheckHealthAsync_WhenIdpIsHealthy_ReturnsHealthy()
    {
        // Arrange
        _idp.HealthCheckAsync(Arg.Any<CancellationToken>())
            .Returns(PortHealthStatus.Healthy);

        var healthCheck = new IdentityProviderHealthCheck(_idp);

        // Act
        var result = await healthCheck.CheckHealthAsync(BuildContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenIdpIsUnhealthy_ReturnsUnhealthy()
    {
        // Arrange
        _idp.HealthCheckAsync(Arg.Any<CancellationToken>())
            .Returns(PortHealthStatus.Unhealthy);

        var healthCheck = new IdentityProviderHealthCheck(_idp);

        // Act
        var result = await healthCheck.CheckHealthAsync(BuildContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenIdpThrows_ReturnsUnhealthyWithoutPropagating()
    {
        // Arrange — exceção inesperada não deve propagar nem expor detalhe interno
        _idp.HealthCheckAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Internal Firebase error"));

        var healthCheck = new IdentityProviderHealthCheck(_idp);

        // Act
        var result = await healthCheck.CheckHealthAsync(BuildContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().NotContain("Firebase");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenIdpIsDegraded_ReturnsDegraded()
    {
        // Arrange
        _idp.HealthCheckAsync(Arg.Any<CancellationToken>())
            .Returns(PortHealthStatus.Degraded);

        var healthCheck = new IdentityProviderHealthCheck(_idp);

        // Act
        var result = await healthCheck.CheckHealthAsync(BuildContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
    }
}
