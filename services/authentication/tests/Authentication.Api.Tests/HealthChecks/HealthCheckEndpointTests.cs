using Authentication.Application.Ports;
using Authentication.Application.Ports.Results;
using Authentication.Api.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;
using System.Net;

namespace Authentication.Api.Tests.HealthChecks;

/// <summary>
/// Testes de integração dos endpoints de health check.
///
/// Verifica que:
///   - GET /health/ready → unhealthy quando IdP falha (para de receber tráfego)
///   - GET /health/ready → unhealthy quando Redis falha
///   - GET /health/live  → healthy mesmo quando IdP falha (não derruba o pod)
///   - Respostas de health check não expõem stack trace ou detalhe interno
///
/// Mapeia: TASK-21, RNF 3.2, design.md § 11, RISK-AUTH-01.
/// </summary>
public sealed class HealthCheckEndpointTests : IClassFixture<HealthCheckApiFactory>
{
    private readonly HealthCheckApiFactory _factory;

    public HealthCheckEndpointTests(HealthCheckApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealthReady_WhenAllHealthy_Returns200()
    {
        // Arrange
        _factory.IdpHealthy = true;
        _factory.RedisHealthy = true;
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHealthReady_WhenIdpUnhealthy_Returns503()
    {
        // Arrange
        _factory.IdpHealthy = false;
        _factory.RedisHealthy = true;
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetHealthReady_WhenRedisUnhealthy_Returns503()
    {
        // Arrange
        _factory.IdpHealthy = true;
        _factory.RedisHealthy = false;
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetHealthLive_WhenIdpUnhealthy_Returns200()
    {
        // Arrange — liveness NÃO depende do IdP (não derruba o pod em falha transitória)
        _factory.IdpHealthy = false;
        _factory.RedisHealthy = true;
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert — liveness sempre healthy quando o próprio processo está vivo
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHealthReady_ResponseBody_DoesNotExposeInternalDetail()
    {
        // Arrange
        _factory.IdpHealthy = false;
        _factory.RedisHealthy = true;
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        // Assert — nunca expõe stack trace, mensagem interna de exceção ou nome de classe SDK
        body.Should().NotContain("StackTrace");
        body.Should().NotContain("Exception");
        body.Should().NotContain("Firebase");
    }
}
