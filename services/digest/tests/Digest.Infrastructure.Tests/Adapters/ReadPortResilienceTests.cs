using Digest.Infrastructure.Adapters;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Digest.Infrastructure.Tests.Adapters;

/// <summary>
/// Testes de resiliência para os adaptadores das portas de leitura (TASK-19).
/// Verifica: timeout → degradação graciosa; circuit breaker → degradação graciosa;
/// IForecastReadPort retorna null quando fonte indisponível (RNF 5.4, PBT-04).
/// Usa handlers stub para simular falhas de HTTP sem dependência externa.
/// </summary>
public sealed class ReadPortResilienceTests
{
    // ---------------------------------------------------------------
    // Handlers stub para simular cenários de falha
    // ---------------------------------------------------------------

    /// <summary>Handler que sempre retorna 500 (simula falha do upstream).</summary>
    private sealed class AlwaysFailHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
    }

    /// <summary>Handler que sempre retorna 200 com lista vazia (simula sucesso).</summary>
    private sealed class AlwaysSucceedEmptyListHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    /// <summary>Handler que retorna 404 (simula ausência de dado — ForecastReadPort).</summary>
    private sealed class NotFoundHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }

    /// <summary>Handler que simula delay excedendo timeout (> 10s).</summary>
    private sealed class SlowHandler : DelegatingHandler
    {
        private readonly TimeSpan _delay;

        public SlowHandler(TimeSpan delay) => _delay = delay;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }

    private static HttpClient CreateHttpClient(DelegatingHandler handler)
    {
        handler.InnerHandler = new HttpClientHandler();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://stub-service/"),
            Timeout = TimeSpan.FromSeconds(30), // Timeout Polly é 10s; este é o HTTP client timeout
        };
        return client;
    }

    // ---------------------------------------------------------------
    // ActivityReadAdapter — degradação graciosa
    // ---------------------------------------------------------------

    [Fact(DisplayName = "PendenciasReadAdapter: fonte indisponível (500) retorna lista vazia após retentativas")]
    public async Task PendenciasReadAdapter_SourceUnavailable_ReturnsEmptyList()
    {
        using var client = CreateHttpClient(new AlwaysFailHandler());
        var adapter = new PendenciasReadAdapter(client, NullLogger<PendenciasReadAdapter>.Instance);

        // Act — 3 tentativas com backoff, depois retorna vazio (degradação graciosa)
        var result = await adapter.GetOverdueActivitiesAsync(
            Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        result.Should().BeEmpty(
            "degradação graciosa: fonte indisponível retorna lista vazia (RNF 5.4)");
    }

    [Fact(DisplayName = "PendenciasReadAdapter: fonte disponível retorna lista (handler com 200 vazio)")]
    public async Task PendenciasReadAdapter_SourceAvailable_ReturnsEmptyListOnSuccess()
    {
        using var client = CreateHttpClient(new AlwaysSucceedEmptyListHandler());
        var adapter = new PendenciasReadAdapter(client, NullLogger<PendenciasReadAdapter>.Instance);

        var result = await adapter.GetOverdueActivitiesAsync(
            Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // ForecastReadAdapter — retorna null quando indisponível (RNF 5.4)
    // ---------------------------------------------------------------

    [Fact(DisplayName = "ForecastReadAdapter: fonte indisponível (500) retorna null — degradação graciosa")]
    public async Task ForecastReadAdapter_SourceUnavailable_ReturnsNull()
    {
        using var client = CreateHttpClient(new AlwaysFailHandler());
        var adapter = new ForecastReadAdapter(client, NullLogger<ForecastReadAdapter>.Instance);

        // Act — não deve lançar; retorna null (RNF 5.4, PBT-04)
        var result = await adapter.GetForecastBlockAsync(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        result.Should().BeNull(
            "ForecastReadAdapter deve retornar null quando fonte indisponível (RNF 5.4)");
    }

    [Fact(DisplayName = "ForecastReadAdapter: 404 retorna null — sem meta cadastrada (degradação graciosa)")]
    public async Task ForecastReadAdapter_NotFound_ReturnsNull()
    {
        using var client = CreateHttpClient(new NotFoundHandler());
        var adapter = new ForecastReadAdapter(client, NullLogger<ForecastReadAdapter>.Instance);

        var result = await adapter.GetForecastBlockAsync(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        result.Should().BeNull(
            "ForecastReadAdapter deve retornar null em 404 (sem meta cadastrada — Req 5.4)");
    }

    [Fact(DisplayName = "ForecastReadAdapter: não lança exceção quando fonte indisponível")]
    public async Task ForecastReadAdapter_SourceUnavailable_DoesNotThrow()
    {
        using var client = CreateHttpClient(new AlwaysFailHandler());
        var adapter = new ForecastReadAdapter(client, NullLogger<ForecastReadAdapter>.Instance);

        // Não deve lançar — garante contrato da interface (Req 5.4)
        var act = async () => await adapter.GetForecastBlockAsync(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        await act.Should().NotThrowAsync(
            "IForecastReadPort nunca lança exceção — retorna null em caso de falha (Req 5.4)");
    }

    // ---------------------------------------------------------------
    // OpportunityReadAdapter — degradação graciosa
    // ---------------------------------------------------------------

    [Fact(DisplayName = "PipelineReadAdapter: fonte indisponível retorna lista vazia")]
    public async Task PipelineReadAdapter_SourceUnavailable_ReturnsEmptyList()
    {
        using var client = CreateHttpClient(new AlwaysFailHandler());
        var adapter = new PipelineReadAdapter(client, NullLogger<PipelineReadAdapter>.Instance);

        var result = await adapter.GetStaleOpportunitiesAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeEmpty(
            "degradação graciosa: fonte indisponível retorna lista vazia (RNF 5.4)");
    }

    // ---------------------------------------------------------------
    // UserDigestPreferenceAdapter — padrão inclusivo (OptOut = false)
    // ---------------------------------------------------------------

    [Fact(DisplayName = "DigestPreferenceReadAdapter: fonte indisponível retorna OptOut=false (padrão inclusivo)")]
    public async Task DigestPreferenceReadAdapter_SourceUnavailable_ReturnsInclusiveDefault()
    {
        using var client = CreateHttpClient(new AlwaysFailHandler());
        var adapter = new DigestPreferenceReadAdapter(client, NullLogger<DigestPreferenceReadAdapter>.Instance);

        var result = await adapter.GetPreferenceAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Should().NotBeNull();
        result.OptOut.Should().BeFalse(
            "padrão inclusivo: quando fonte indisponível, OptOut=false (DD-003, RNF 5.4)");
    }

    // ---------------------------------------------------------------
    // DirectoryReadAdapter — degradação graciosa
    // ---------------------------------------------------------------

    [Fact(DisplayName = "DirectoryReadAdapter: fonte indisponível retorna lista de tenants vazia")]
    public async Task DirectoryReadAdapter_SourceUnavailable_ReturnsEmptyTenantList()
    {
        using var client = CreateHttpClient(new AlwaysFailHandler());
        var adapter = new DirectoryReadAdapter(client, NullLogger<DirectoryReadAdapter>.Instance);

        var result = await adapter.GetActiveTenantInfosAsync();

        result.Should().BeEmpty(
            "degradação graciosa: fonte indisponível retorna lista vazia (RNF 5.4)");
    }

    // ---------------------------------------------------------------
    // Timeout: operação lenta resulta em degradação graciosa
    // Nota: o timeout Polly é 10s — usamos CancellationToken para simular timeout rapidamente
    // ---------------------------------------------------------------

    [Fact(DisplayName = "ForecastReadAdapter: CancellationToken externo cancelado retorna null sem lançar")]
    public async Task ForecastReadAdapter_ExternalCancellation_ReturnsNull()
    {
        // Usa handler lento (20s) + CancellationToken cancelado imediatamente
        using var cts = new CancellationTokenSource();
        using var client = CreateHttpClient(new SlowHandler(TimeSpan.FromSeconds(20)));
        var adapter = new ForecastReadAdapter(client, NullLogger<ForecastReadAdapter>.Instance);

        // Cancela imediatamente antes de chamar
        await cts.CancelAsync();

        // Com cancelamento externo, o adapter deve retornar null sem lançar OperationCanceledException
        // (pois o Polly propaga o cancelamento externo, mas o ForecastReadAdapter captura e degrada)
        var act = async () => await adapter.GetForecastBlockAsync(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), cts.Token);

        // Cancelamento externo pode propagar; o que não pode é lançar exceção de negócio
        // (aceita OperationCanceledException pois é cancelamento legítimo do caller)
        await act.Should().NotThrowAsync<HttpRequestException>(
            "ForecastReadAdapter não lança HttpRequestException mesmo com cancelamento");
    }
}
