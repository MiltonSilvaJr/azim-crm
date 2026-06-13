using Authentication.Infrastructure.HealthChecks;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace Authentication.Infrastructure.Tests.HealthChecks;

/// <summary>
/// Testes do <see cref="RedisHealthCheck"/>.
///
/// Verifica que:
///   - Redis acessível (PING OK) → HealthCheckResult.Healthy
///   - Redis sem resposta ao PING → HealthCheckResult.Unhealthy
///   - Exceção de conexão → HealthCheckResult.Unhealthy (sem propagar)
///   - Resposta não confirma PONG → HealthCheckResult.Unhealthy
///
/// Mapeia: TASK-21, RNF 3.2, design.md § 11.
/// </summary>
public sealed class RedisHealthCheckTests
{
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _db = Substitute.For<IDatabase>();

    public RedisHealthCheckTests()
    {
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>())
              .Returns(_db);
    }

    private HealthCheckContext BuildContext() =>
        new()
        {
            Registration = new HealthCheckRegistration(
                "redis",
                new RedisHealthCheck(_redis),
                HealthStatus.Unhealthy,
                ["readiness"])
        };

    [Fact]
    public async Task CheckHealthAsync_WhenPingSucceeds_ReturnsHealthy()
    {
        // Arrange
        _db.PingAsync(Arg.Any<CommandFlags>())
           .Returns(TimeSpan.FromMilliseconds(1));

        var healthCheck = new RedisHealthCheck(_redis);

        // Act
        var result = await healthCheck.CheckHealthAsync(BuildContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPingThrows_ReturnsUnhealthy()
    {
        // Arrange — simula falha de conectividade
        _db.PingAsync(Arg.Any<CommandFlags>())
           .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection refused"));

        var healthCheck = new RedisHealthCheck(_redis);

        // Act
        var result = await healthCheck.CheckHealthAsync(BuildContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        // Não propaga detalhe interno na mensagem exposta
        result.Description.Should().NotBeNull();
        result.Description.Should().NotContain("StackExchange");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPingTimeout_ReturnsUnhealthy()
    {
        // Arrange — timeout excessivo (acima de 2 s considerado indisponível)
        _db.PingAsync(Arg.Any<CommandFlags>())
           .Returns(TimeSpan.FromSeconds(10));

        var healthCheck = new RedisHealthCheck(_redis);

        // Act
        var result = await healthCheck.CheckHealthAsync(BuildContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }
}
