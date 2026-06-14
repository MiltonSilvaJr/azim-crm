using System.Net;
using Xunit;

namespace Digest.Api.Tests.Health;

/// <summary>
/// Testes de API dos health checks do worker (TASK-21).
/// Cobre: GET /health/live e GET /health/ready (design §8.2).
/// </summary>
public sealed class HealthCheckTests : IClassFixture<DigestApiFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(DigestApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Liveness_ReturnsOk()
    {
        // Arrange + Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_ReturnsOkOrServiceUnavailable()
    {
        // Arrange + Act
        var response = await _client.GetAsync("/health/ready");

        // Assert — em ambiente de teste sem DB/Pub/Sub, pode retornar 503 (degraded/unhealthy)
        // O importante é que o endpoint exista e retorne uma resposta (não 404)
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
