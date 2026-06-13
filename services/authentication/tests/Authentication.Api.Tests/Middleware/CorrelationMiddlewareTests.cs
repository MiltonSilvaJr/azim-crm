using System.Net;
using Authentication.Api.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Authentication.Api.Tests.Middleware;

/// <summary>
/// Testes de integração para o <c>CorrelationMiddleware</c>.
///
/// Verifica:
///   - correlationId presente no header de resposta (X-Correlation-ID).
///   - correlationId gerado quando ausente na requisição.
///   - correlationId propagado quando presente na requisição.
///
/// Mapeia: TASK-15, design.md § 5.4 (1. CorrelationMiddleware), RNF 4.1.
/// </summary>
public sealed class CorrelationMiddlewareTests : IClassFixture<AuthenticationApiFactory>
{
    private readonly HttpClient _client;

    public CorrelationMiddlewareTests(AuthenticationApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact(DisplayName = "Toda requisição recebe X-Correlation-ID no header de resposta (RNF 4.1)")]
    public async Task Request_Always_HasCorrelationIdInResponseHeader()
    {
        var response = await _client.GetAsync("/health/ping");

        response.Headers.Should().ContainKey("X-Correlation-ID",
            because: "CorrelationMiddleware deve garantir correlationId em 100% das requisições (RNF 4.1)");
    }

    [Fact(DisplayName = "X-Correlation-ID é propagado quando enviado na requisição")]
    public async Task CorrelationId_IsEchoed_WhenSentInRequest()
    {
        var correlationId = Guid.NewGuid().ToString();
        _client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

        var response = await _client.GetAsync("/health/ping");

        response.Headers.TryGetValues("X-Correlation-ID", out var values).Should().BeTrue();
        values.Should().ContainSingle(v => v == correlationId,
            because: "CorrelationMiddleware deve propagar o correlationId recebido (RNF 4.1)");

        _client.DefaultRequestHeaders.Remove("X-Correlation-ID");
    }

    [Fact(DisplayName = "X-Correlation-ID é gerado quando não enviado na requisição")]
    public async Task CorrelationId_IsGenerated_WhenNotSentInRequest()
    {
        var response = await _client.GetAsync("/health/ping");

        response.Headers.TryGetValues("X-Correlation-ID", out var values).Should().BeTrue();
        var id = values!.First();
        id.Should().NotBeNullOrWhiteSpace(
            because: "CorrelationMiddleware deve gerar um correlationId quando ausente na requisição");
        Guid.TryParse(id, out _).Should().BeTrue(
            because: "correlationId gerado deve ser um UUID válido");
    }
}
