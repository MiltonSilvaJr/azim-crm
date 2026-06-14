using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using GoalForecast.Api.Tests.Infrastructure;
using GoalForecast.Application.Queries;
using NSubstitute;

namespace GoalForecast.Api.Tests.Performance;

/// <summary>
/// Baseline de performance do painel comparativo — TASK-29.
///
/// Objetivo: verificar que o handler do painel (GET /api/v1/forecast) atende
/// ao SLO p95 ≤ 3.000 ms definido no RNF-3.1 e design §15.
///
/// Metodologia:
/// Esta suite usa WebApplicationFactory com handlers mockados (NSubstitute),
/// medindo apenas a latência da camada HTTP/middleware/controller da aplicação —
/// sem banco de dados real nem pipeline real. É um teste de referência de overhead
/// da stack .NET, não um teste de carga fim a fim.
///
/// Para medir o SLO real (incluindo banco e pipeline), usar ferramenta externa
/// (k6, NBomber) contra ambiente integrado com Testcontainers.
///
/// Resultado de referência (2026-06-14, net10.0, MacBook ARM):
///   Iterações: 100 | p50: &lt;10ms | p95: &lt;50ms | p99: &lt;100ms
///   (muito abaixo do SLO de 3.000ms; overhead do banco/pipeline é o gargalo esperado)
///
/// Mapeia: TASK-29, RNF-3.1, design §15.
/// </summary>
public sealed class PerformanceBaselineTests : IClassFixture<GoalForecastWebFactory>
{
    private const int Iterations = 100;
    private const double SloP95Ms = 3_000.0; // RNF-3.1: p95 ≤ 3.000 ms

    private readonly GoalForecastWebFactory _factory;
    private readonly HttpClient _client;

    public PerformanceBaselineTests(GoalForecastWebFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        // Configura handler para responder de forma determinística (sem I/O real)
        _factory.ForecastPanelHandler
            .Handle(Arg.Any<GetForecastPanelQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ForecastPanelResult(
                Scope: "BU",
                BuId: TestAuthHelper.BuA1,
                OwnerId: null,
                Year: 2026,
                Month: 1,
                ValorMeta: 100_000_00L,
                Realizado: 60_000_00L,
                PipelineDisponivel: 40_000_00L,
                Gap: -40_000_00L,
                PctAtingimento: 0.6,
                PipelineUnavailable: false));
    }

    /// <summary>
    /// Mede o p95 de latência de GET /forecast em cenário de stack (sem banco real).
    /// Garante que o overhead da camada HTTP+middleware está muito abaixo do SLO.
    ///
    /// Nota: o SLO real (incluindo banco + pipeline) deve ser validado em ambiente
    /// integrado com Testcontainers/k6 (TASK-29 ST-02 completo). Este teste valida
    /// apenas o overhead da aplicação, que deve ser ≪ 3.000 ms.
    /// </summary>
    [Fact(DisplayName = "GET /forecast p95 de latência (stack only, sem banco) deve ser ≪ 3.000 ms (SLO RNF-3.1)")]
    public async Task GetForecast_P95Latency_AbaixoDoSlo()
    {
        // ── Aquecimento (warm-up) ──────────────────────────────────────────────
        for (var i = 0; i < 10; i++)
            await SendForecastRequest();

        // ── Medição ────────────────────────────────────────────────────────────
        var latencies = new List<double>(Iterations);
        for (var i = 0; i < Iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var response = await SendForecastRequest();
            sw.Stop();

            response.StatusCode.Should().Be(HttpStatusCode.OK,
                because: "o endpoint deve retornar 200 em todas as iterações");

            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // ── Estatísticas ───────────────────────────────────────────────────────
        latencies.Sort();
        var p50 = Percentile(latencies, 50);
        var p95 = Percentile(latencies, 95);
        var p99 = Percentile(latencies, 99);
        var min = latencies[0];
        var max = latencies[^1];

        // Registro do resultado (visível no output do test runner)
        var summary = $"""
            [TASK-29] Baseline de performance — GET /forecast (stack only, sem banco real)
            Iterações : {Iterations}
            Min       : {min:F1} ms
            p50       : {p50:F1} ms
            p95       : {p95:F1} ms
            p99       : {p99:F1} ms
            Max       : {max:F1} ms
            SLO p95   : {SloP95Ms:F0} ms (RNF-3.1)
            Status    : {(p95 <= SloP95Ms ? "VERDE" : "RISCO")}

            Nota: este baseline mede apenas o overhead da camada HTTP+middleware.
            O gargalo esperado em produção é a leitura do banco e do pipeline (design §15).
            Para validação fim a fim, executar k6/NBomber contra ambiente com Testcontainers.
            """;

        // O p95 de overhead da stack deve ficar muito abaixo do SLO real de 3.000 ms.
        // Se exceder, investigar regressão de performance na camada middleware/serialização.
        p95.Should().BeLessThan(SloP95Ms,
            because: $"o overhead da stack não deve exceder o SLO de {SloP95Ms}ms. " +
                     $"Resultado: p95={p95:F1}ms. Resumo:\n{summary}");

        // Saída para o test runner (visível no dotnet test --logger)
        Console.WriteLine(summary);
    }

    /// <summary>
    /// Verifica que múltiplas requisições simultâneas ao painel não degradam a latência
    /// além do SLO. Simula concorrência básica sem banco real.
    /// </summary>
    [Fact(DisplayName = "GET /forecast com 10 requisições concorrentes — sem degradação significativa")]
    public async Task GetForecast_ConcurrentRequests_SemDegradacaoSignificativa()
    {
        const int concurrency = 10;
        const int perClient = 20;

        var tasks = Enumerable.Range(0, concurrency).Select(async _ =>
        {
            var latencies = new List<double>(perClient);
            for (var i = 0; i < perClient; i++)
            {
                var sw = Stopwatch.StartNew();
                var response = await SendForecastRequest();
                sw.Stop();
                response.StatusCode.Should().Be(HttpStatusCode.OK);
                latencies.Add(sw.Elapsed.TotalMilliseconds);
            }
            return latencies;
        });

        var results = await Task.WhenAll(tasks);
        var allLatencies = results.SelectMany(l => l).OrderBy(x => x).ToList();

        var p95 = Percentile(allLatencies, 95);

        p95.Should().BeLessThan(SloP95Ms,
            because: $"p95 de latência com {concurrency} clientes concorrentes não deve exceder {SloP95Ms}ms. " +
                     $"Obtido: {p95:F1}ms");

        Console.WriteLine($"[TASK-29] Concorrência: {concurrency} clientes × {perClient} req. " +
                          $"p95={p95:F1}ms (SLO={SloP95Ms}ms)");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendForecastRequest()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/forecast?buId={TestAuthHelper.BuA1}&year=2026&month=1");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);
        return await _client.SendAsync(request);
    }

    private static double Percentile(List<double> sorted, int percentile)
    {
        if (sorted.Count == 0) return 0;
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }
}
