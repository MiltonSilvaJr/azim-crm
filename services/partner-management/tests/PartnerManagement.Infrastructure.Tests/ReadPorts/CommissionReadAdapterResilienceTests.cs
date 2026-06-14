using FluentAssertions;
using PartnerManagement.Application.Ports;
using PartnerManagement.Infrastructure.ReadPorts;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using System.Net;
using Xunit;

namespace PartnerManagement.Infrastructure.Tests.ReadPorts;

/// <summary>
/// Testes de resiliência do <see cref="PartnerCommissionReadAdapter"/>.
/// Verifica timeout, retry com backoff e degradação parcial quando o pipeline
/// estiver indisponível. Usa <see cref="StubbedHandler"/> em memória — sem rede real.
/// Os testes que envolvem retry e timeout usam o construtor internal do adapter para
/// injetar um pipeline sem delay (testes rápidos e determinísticos).
///
/// Mapeia: TASK-28, RISK-PM-06, design §15, design §6.4, DD-003.
/// </summary>
public sealed class CommissionReadAdapterResilienceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid PartnerId = Guid.NewGuid();
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 3, 31, 23, 59, 59, TimeSpan.Zero);

    // =========================================================================
    // ST-01 / TASK-28: degradação quando pipeline retorna 500
    // =========================================================================

    [Fact(DisplayName = "TASK-28: Read port retornando 500 retorna lista vazia (degradação)")]
    public async Task GetCommissionLines_When500_ReturnsEmpty()
    {
        // Arrange — handler sempre retorna 500; sem retry para testes rápidos
        PartnerCommissionReadAdapter adapter = BuildAdapter(
            new StubbedHandler(HttpStatusCode.InternalServerError),
            BuildNoRetryPipeline());

        // Act — não deve lançar exceção
        IReadOnlyList<CommissionLine> result = await adapter.GetCommissionLinesAsync(
            TenantId, PartnerId, From, To, correlationId: "test-corr", CancellationToken.None);

        // Assert — degradação parcial: lista vazia, sem exceção
        result.Should().BeEmpty("o adapter deve retornar lista vazia em caso de 500 (degradação parcial)");
    }

    // =========================================================================
    // TASK-28: degradação quando pipeline retorna 503
    // =========================================================================

    [Fact(DisplayName = "TASK-28: Read port retornando 503 retorna lista vazia (degradação)")]
    public async Task GetCommissionLines_When503_ReturnsEmpty()
    {
        // Arrange
        PartnerCommissionReadAdapter adapter = BuildAdapter(
            new StubbedHandler(HttpStatusCode.ServiceUnavailable),
            BuildNoRetryPipeline());

        // Act
        IReadOnlyList<CommissionLine> result = await adapter.GetCommissionLinesAsync(
            TenantId, PartnerId, From, To, correlationId: null, CancellationToken.None);

        // Assert
        result.Should().BeEmpty("503 deve acionar degradação parcial");
    }

    // =========================================================================
    // TASK-28: resposta de sucesso retorna linhas mapeadas corretamente
    // =========================================================================

    [Fact(DisplayName = "TASK-28: Read port retornando 200 com linhas retorna CommissionLines mapeadas")]
    public async Task GetCommissionLines_When200WithLines_ReturnsMappedLines()
    {
        // Arrange — resposta JSON do pipeline
        Guid oppId = Guid.NewGuid();
        string json = $$"""
            [
                {
                    "opportunityId": "{{oppId}}",
                    "commissionCents": 15000,
                    "isSnapshot": true,
                    "occurredAt": "2026-02-15T00:00:00+00:00"
                }
            ]
            """;

        PartnerCommissionReadAdapter adapter = BuildAdapter(
            new StubbedHandler(HttpStatusCode.OK, json),
            BuildNoRetryPipeline());

        // Act
        IReadOnlyList<CommissionLine> result = await adapter.GetCommissionLinesAsync(
            TenantId, PartnerId, From, To, correlationId: "corr-42", CancellationToken.None);

        // Assert
        result.Should().HaveCount(1, "deve mapear 1 linha retornada pelo pipeline");
        result[0].OpportunityId.Should().Be(oppId);
        result[0].CommissionCents.Should().Be(15000L);
        result[0].IsSnapshot.Should().BeTrue();
    }

    // =========================================================================
    // TASK-28: headers de rastreabilidade são propagados ao pipeline
    // =========================================================================

    [Fact(DisplayName = "TASK-28: Correlation-Id e Tenant-Id são propagados ao pipeline")]
    public async Task GetCommissionLines_PropagatesTraceabilityHeaders()
    {
        // Arrange
        CapturingHandler capturingHandler = new(HttpStatusCode.OK, "[]");
        PartnerCommissionReadAdapter adapter = BuildAdapter(capturingHandler, BuildNoRetryPipeline());

        string correlationId = "corr-xyz-789";

        // Act
        await adapter.GetCommissionLinesAsync(TenantId, PartnerId, From, To, correlationId, CancellationToken.None);

        // Assert
        capturingHandler.CapturedRequest.Should().NotBeNull();
        capturingHandler.CapturedRequest!.Headers
            .Should().ContainKey("X-Tenant-Id", "tenant_id deve ser propagado");
        capturingHandler.CapturedRequest.Headers.GetValues("X-Tenant-Id")
            .Should().Contain(TenantId.ToString());
        capturingHandler.CapturedRequest.Headers
            .Should().ContainKey("X-Correlation-Id", "correlation_id deve ser propagado");
        capturingHandler.CapturedRequest.Headers.GetValues("X-Correlation-Id")
            .Should().Contain(correlationId);
    }

    // =========================================================================
    // TASK-28: retry é acionado em falhas transitórias e recupera na retentativa
    // =========================================================================

    [Fact(DisplayName = "TASK-28: Retry acionado — falha transitória seguida de sucesso retorna linhas")]
    public async Task GetCommissionLines_TransientFailureThenSuccess_RetriesAndReturnsLines()
    {
        // Arrange — falha uma vez (500) e depois responde 200 com uma linha
        Guid oppId = Guid.NewGuid();
        string successJson = $$"""
            [{"opportunityId":"{{oppId}}","commissionCents":5000,"isSnapshot":false,"occurredAt":"2026-01-10T00:00:00+00:00"}]
            """;

        FlakyHandler flakyHandler = new(
            failFirstN: 1,
            failureStatus: HttpStatusCode.InternalServerError,
            successJson: successJson);

        // Pipeline com retry mas sem delay para que o teste seja rápido
        ResiliencePipeline<HttpResponseMessage> retryPipeline =
            new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = 2,
                    Delay = TimeSpan.Zero,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .HandleResult(r => !r.IsSuccessStatusCode)
                })
                .Build();

        PartnerCommissionReadAdapter adapter = BuildAdapter(flakyHandler, retryPipeline);

        // Act — após retry, deve ter sucesso
        IReadOnlyList<CommissionLine> result = await adapter.GetCommissionLinesAsync(
            TenantId, PartnerId, From, To, correlationId: null, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1, "após retry bem-sucedido, deve retornar a linha");
        result[0].OpportunityId.Should().Be(oppId);
        flakyHandler.CallCount.Should().BeGreaterThanOrEqualTo(2, "deve ter sido chamado ao menos 2 vezes (1 falha + 1 sucesso)");
    }

    // =========================================================================
    // TASK-28: timeout Polly propaga como degradação
    // =========================================================================

    [Fact(DisplayName = "TASK-28: Timeout Polly do read port resulta em lista vazia (degradação)")]
    public async Task GetCommissionLines_PollyTimeout_ReturnsEmpty()
    {
        // Arrange — handler que demora mais que o timeout Polly configurado
        TimeoutHandler timeoutHandler = new(delay: TimeSpan.FromSeconds(10));

        // Pipeline com timeout curto (sem retry) para que o teste seja instantâneo
        ResiliencePipeline<HttpResponseMessage> timeoutPipeline =
            new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddTimeout(TimeSpan.FromMilliseconds(50))
                .Build();

        PartnerCommissionReadAdapter adapter = BuildAdapter(timeoutHandler, timeoutPipeline);

        // Act — não deve lançar; deve degradar
        IReadOnlyList<CommissionLine> result = await adapter.GetCommissionLinesAsync(
            TenantId, PartnerId, From, To, correlationId: null, CancellationToken.None);

        // Assert
        result.Should().BeEmpty("timeout Polly deve acionar degradação parcial");
    }

    // =========================================================================
    // Fábrica: constrói o adapter com HttpClient usando o handler stub
    // =========================================================================

    private static PartnerCommissionReadAdapter BuildAdapter(
        HttpMessageHandler handler,
        ResiliencePipeline<HttpResponseMessage> pipeline)
    {
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://pipeline.local/")
        };
        return new PartnerCommissionReadAdapter(httpClient, pipeline);
    }

    /// <summary>
    /// Pipeline sem retry e sem timeout — usado em testes que não validam resiliência,
    /// evitando flakiness por delay ou estado compartilhado de circuit breaker.
    /// </summary>
    private static ResiliencePipeline<HttpResponseMessage> BuildNoRetryPipeline() =>
        ResiliencePipeline<HttpResponseMessage>.Empty;
}

// =========================================================================
// Stubs de HttpMessageHandler para simular cenários
// =========================================================================

/// <summary>
/// Handler stub que retorna sempre o mesmo status e body configurados.
/// </summary>
internal sealed class StubbedHandler(HttpStatusCode statusCode, string? body = null)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        StringContent? content = body is not null
            ? new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            : null;

        HttpResponseMessage response = new(statusCode)
        {
            Content = content ?? new StringContent(string.Empty)
        };

        return Task.FromResult(response);
    }
}

/// <summary>
/// Handler que captura a request recebida para inspeção de headers.
/// </summary>
internal sealed class CapturingHandler(HttpStatusCode statusCode, string body)
    : HttpMessageHandler
{
    public HttpRequestMessage? CapturedRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CapturedRequest = request;
        HttpResponseMessage response = new(statusCode)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

/// <summary>
/// Handler que falha nas primeiras <c>failFirstN</c> chamadas e depois responde com sucesso.
/// Usado para simular falhas transitórias e validar retry.
/// </summary>
internal sealed class FlakyHandler(int failFirstN, HttpStatusCode failureStatus, string successJson)
    : HttpMessageHandler
{
    private int _callCount;

    /// <summary>Número de chamadas realizadas (incluindo tentativas falhas).</summary>
    public int CallCount => _callCount;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        int count = Interlocked.Increment(ref _callCount);

        if (count <= failFirstN)
        {
            return Task.FromResult(new HttpResponseMessage(failureStatus)
            {
                Content = new StringContent(string.Empty)
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(successJson, System.Text.Encoding.UTF8, "application/json")
        });
    }
}

/// <summary>
/// Handler que atrasa a resposta para simular timeout do Polly.
/// </summary>
internal sealed class TimeoutHandler(TimeSpan delay) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
        };
    }
}
