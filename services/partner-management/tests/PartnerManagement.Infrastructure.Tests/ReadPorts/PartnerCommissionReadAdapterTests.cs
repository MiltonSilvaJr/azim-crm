using System.Net;
using System.Text.Json;
using FluentAssertions;
using PartnerManagement.Application.Ports;
using PartnerManagement.Infrastructure.ReadPorts;
using Xunit;

namespace PartnerManagement.Infrastructure.Tests.ReadPorts;

/// <summary>
/// Testes unitários para <see cref="PartnerCommissionReadAdapter"/> (TASK-19).
/// Usa <see cref="StubHttpMessageHandler"/> para interceptar chamadas HTTP sem servidor real.
/// Verifica: headers propagados, mapeamento de DTO, lista vazia em não-2xx, DD-003.
/// Mapeia: Req 9, Req 10, DD-003, design §6.4, TASK-19.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PartnerCommissionReadAdapterTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid PartnerId = Guid.NewGuid();
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 3, 31, 23, 59, 59, TimeSpan.Zero);

    // =========================================================================
    // Headers de rastreabilidade
    // =========================================================================

    /// <summary>
    /// TASK-19: adapter propaga X-Tenant-Id e X-Correlation-Id em toda chamada.
    /// </summary>
    [Fact(DisplayName = "TASK-19: adapter propaga X-Tenant-Id e X-Correlation-Id")]
    public async Task Adapter_PropagatesCorrelationAndTenantHeaders()
    {
        // Arrange
        string correlationId = "corr-abc-123";

        HttpRequestMessage? capturedRequest = null;

        StubHttpMessageHandler handler = new(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            };
        });

        PartnerCommissionReadAdapter adapter = CreateAdapter(handler, "https://pipeline.internal/");

        // Act
        await adapter.GetCommissionLinesAsync(TenantId, PartnerId, From, To, correlationId);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().ContainKey("X-Tenant-Id");
        capturedRequest.Headers.GetValues("X-Tenant-Id").Should().Contain(TenantId.ToString());
        capturedRequest.Headers.Should().ContainKey("X-Correlation-Id");
        capturedRequest.Headers.GetValues("X-Correlation-Id").Should().Contain(correlationId);
    }

    /// <summary>
    /// TASK-19: adapter não envia X-Correlation-Id quando correlationId é null.
    /// </summary>
    [Fact(DisplayName = "TASK-19: adapter omite X-Correlation-Id quando null")]
    public async Task Adapter_OmitsCorrelationIdHeader_WhenNull()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;

        StubHttpMessageHandler handler = new(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            };
        });

        PartnerCommissionReadAdapter adapter = CreateAdapter(handler, "https://pipeline.internal/");

        // Act
        await adapter.GetCommissionLinesAsync(TenantId, PartnerId, From, To, correlationId: null);

        // Assert
        capturedRequest!.Headers.Contains("X-Correlation-Id")
            .Should().BeFalse("correlationId null não deve gerar header");
    }

    // =========================================================================
    // Mapeamento de DTO
    // =========================================================================

    /// <summary>
    /// TASK-19: adapter desserializa CommissionLineDto e mapeia para CommissionLine (DD-003).
    /// </summary>
    [Fact(DisplayName = "TASK-19: adapter mapeia CommissionLineDto para CommissionLine")]
    public async Task Adapter_MapsDto_ToCommissionLine()
    {
        // Arrange
        Guid opportunityId = Guid.NewGuid();
        DateTimeOffset occurredAt = new(2026, 2, 15, 10, 0, 0, TimeSpan.Zero);

        var dtos = new[]
        {
            new
            {
                opportunityId,
                commissionCents = 15000L,
                isSnapshot = false,
                occurredAt
            }
        };

        string json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        StubHttpMessageHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

        PartnerCommissionReadAdapter adapter = CreateAdapter(handler, "https://pipeline.internal/");

        // Act
        IReadOnlyList<CommissionLine> result = await adapter.GetCommissionLinesAsync(
            TenantId, PartnerId, From, To, correlationId: null);

        // Assert — sem soma no adapter (DD-003); apenas mapeamento
        result.Should().HaveCount(1);
        result[0].OpportunityId.Should().Be(opportunityId);
        result[0].CommissionCents.Should().Be(15000L);
        result[0].IsSnapshot.Should().BeFalse();
        result[0].OccurredAt.Should().Be(occurredAt);
    }

    // =========================================================================
    // Degradação: não-2xx retorna lista vazia
    // =========================================================================

    /// <summary>
    /// TASK-19: adapter retorna lista vazia quando pipeline responde com 503 (degradação parcial).
    /// </summary>
    [Fact(DisplayName = "TASK-19: adapter retorna lista vazia em resposta não-2xx")]
    public async Task Adapter_ReturnsEmptyList_WhenPipelineReturns503()
    {
        // Arrange
        StubHttpMessageHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        PartnerCommissionReadAdapter adapter = CreateAdapter(handler, "https://pipeline.internal/");

        // Act
        IReadOnlyList<CommissionLine> result = await adapter.GetCommissionLinesAsync(
            TenantId, PartnerId, From, To, correlationId: null);

        // Assert — degradação parcial: retorna vazio sem lançar exceção
        result.Should().BeEmpty("adapter deve retornar vazio em degradação, não lançar exceção");
    }

    /// <summary>
    /// TASK-19: adapter retorna lista vazia quando pipeline responde com 404.
    /// </summary>
    [Fact(DisplayName = "TASK-19: adapter retorna lista vazia em resposta 404")]
    public async Task Adapter_ReturnsEmptyList_WhenPipelineReturns404()
    {
        // Arrange
        StubHttpMessageHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        PartnerCommissionReadAdapter adapter = CreateAdapter(handler, "https://pipeline.internal/");

        // Act
        IReadOnlyList<CommissionLine> result = await adapter.GetCommissionLinesAsync(
            TenantId, PartnerId, From, To, correlationId: null);

        // Assert
        result.Should().BeEmpty();
    }

    /// <summary>
    /// TASK-19: adapter retorna lista vazia quando pipeline retorna array JSON vazio.
    /// </summary>
    [Fact(DisplayName = "TASK-19: adapter retorna lista vazia quando pipeline retorna array vazio")]
    public async Task Adapter_ReturnsEmptyList_WhenPipelineReturnsEmptyArray()
    {
        // Arrange
        StubHttpMessageHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
        });

        PartnerCommissionReadAdapter adapter = CreateAdapter(handler, "https://pipeline.internal/");

        // Act
        IReadOnlyList<CommissionLine> result = await adapter.GetCommissionLinesAsync(
            TenantId, PartnerId, From, To, correlationId: null);

        // Assert
        result.Should().BeEmpty();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static PartnerCommissionReadAdapter CreateAdapter(
        HttpMessageHandler handler,
        string baseAddress)
    {
        HttpClient client = new(handler) { BaseAddress = new Uri(baseAddress) };
        return new PartnerCommissionReadAdapter(client);
    }

    /// <summary>
    /// HttpMessageHandler stub que delega para um Func — sem necessidade de WireMock.
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
