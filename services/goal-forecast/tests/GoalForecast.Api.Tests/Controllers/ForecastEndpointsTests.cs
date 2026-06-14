using System.Net;
using System.Net.Http.Json;
using GoalForecast.Api.Tests.Infrastructure;
using GoalForecast.Application.Queries;
using NSubstitute;

namespace GoalForecast.Api.Tests.Controllers;

/// <summary>
/// Testes de API para GET /api/v1/forecast e GET /api/v1/forecast/aggregate.
/// Verifica operação total (nunca 404), degradação graciosa, NaN-free, long.
///
/// Mapeia: TASK-24, design §8.4, §8.5, Req 5, Req 6, Req 7, PBT-03, PBT-04.
/// </summary>
public sealed class ForecastEndpointsTests : IClassFixture<GoalForecastWebFactory>
{
    private readonly GoalForecastWebFactory _factory;
    private readonly HttpClient _client;

    public ForecastEndpointsTests(GoalForecastWebFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Autenticação ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetForecast_SemAuth_Retorna401()
    {
        var response = await _client.GetAsync(
            $"/api/v1/forecast?buId={TestAuthHelper.BuA1}&year=2026&month=1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAggregate_SemAuth_Retorna401()
    {
        var response = await _client.GetAsync(
            $"/api/v1/forecast/aggregate?buId={TestAuthHelper.BuA1}&year=2026&granularity=quarter&quarter=1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Painel sem meta (DD-006) ──────────────────────────────────────────────

    [Fact]
    public async Task GetForecast_SemMeta_Retorna200ComValorMetaNulo()
    {
        // Arrange: sem meta cadastrada — ValorMeta, Gap, PctAtingimento nulos
        _factory.ForecastPanelHandler
            .Handle(Arg.Any<GetForecastPanelQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ForecastPanelResult(
                Scope: "BU",
                BuId: TestAuthHelper.BuA1,
                OwnerId: null,
                Year: 2026,
                Month: 1,
                ValorMeta: null,
                Realizado: 50_000_00L,
                PipelineDisponivel: 30_000_00L,
                Gap: null,
                PctAtingimento: null,
                PipelineUnavailable: false));

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/forecast?buId={TestAuthHelper.BuA1}&year=2026&month=1");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — nunca 404; 200 mesmo sem meta (PBT-04, operação total)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ForecastPanelBody>();
        body!.ValorMeta.Should().BeNull();
        body.Gap.Should().BeNull();
        body.PctAtingimento.Should().BeNull();
        body.Realizado.Should().Be(50_000_00L);
        body.PipelineUnavailable.Should().BeFalse();
    }

    // ── Painel com pipeline indisponível (DD-007) ─────────────────────────────

    [Fact]
    public async Task GetForecast_PipelineIndisponivel_Retorna200ComPipelineUnavailableTrue()
    {
        _factory.ForecastPanelHandler
            .Handle(Arg.Any<GetForecastPanelQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ForecastPanelResult(
                Scope: "BU",
                BuId: TestAuthHelper.BuA1,
                OwnerId: null,
                Year: 2026,
                Month: 2,
                ValorMeta: 100_000_00L,
                Realizado: null,
                PipelineDisponivel: null,
                Gap: null,
                PctAtingimento: null,
                PipelineUnavailable: true));

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/forecast?buId={TestAuthHelper.BuA1}&year=2026&month=2");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — nunca 5xx; 200 com pipelineUnavailable=true
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ForecastPanelBody>();
        body!.PipelineUnavailable.Should().BeTrue();
        body.Realizado.Should().BeNull();
        body.PipelineDisponivel.Should().BeNull();
        body.ValorMeta.Should().Be(100_000_00L);
    }

    // ── Painel completo ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetForecast_ComMeta_Retorna200ComTodosCampos()
    {
        _factory.ForecastPanelHandler
            .Handle(Arg.Any<GetForecastPanelQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ForecastPanelResult(
                Scope: "BU",
                BuId: TestAuthHelper.BuA1,
                OwnerId: null,
                Year: 2026,
                Month: 3,
                ValorMeta: 100_000_00L,
                Realizado: 60_000_00L,
                PipelineDisponivel: 40_000_00L,
                Gap: -40_000_00L,
                PctAtingimento: 0.6,
                PipelineUnavailable: false));

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/forecast?buId={TestAuthHelper.BuA1}&year=2026&month=3");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();

        // pctAtingimento nunca deve ser NaN no JSON
        raw.Should().NotContain("NaN");
        raw.Should().NotContain("Infinity");
        raw.Should().NotContain("nan");

        var body = await System.Text.Json.JsonSerializer.DeserializeAsync<ForecastPanelBody>(
            new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(raw)),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        body!.ValorMeta.Should().Be(100_000_00L);
        body.Realizado.Should().Be(60_000_00L);
        body.Gap.Should().Be(-40_000_00L);
    }

    // ── PctAtingimento nunca NaN ──────────────────────────────────────────────

    [Fact]
    public async Task GetForecast_PctAtingimentoNulo_NaoApareceNaNNoJson()
    {
        _factory.ForecastPanelHandler
            .Handle(Arg.Any<GetForecastPanelQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ForecastPanelResult(
                Scope: "BU",
                BuId: TestAuthHelper.BuA1,
                OwnerId: null,
                Year: 2026,
                Month: 4,
                ValorMeta: null,
                Realizado: null,
                PipelineDisponivel: null,
                Gap: null,
                PctAtingimento: null,
                PipelineUnavailable: true));

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/forecast?buId={TestAuthHelper.BuA1}&year=2026&month=4");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();

        raw.Should().NotContain("NaN");
        raw.Should().NotContain("Infinity");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Parâmetros obrigatórios ───────────────────────────────────────────────

    [Fact]
    public async Task GetForecast_SemBuId_Retorna400()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "/api/v1/forecast?year=2026&month=1");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Aggregate ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAggregate_GranularidadeQuarter_Retorna200ComValorLong()
    {
        _factory.AggregateHandler
            .Handle(Arg.Any<GetGoalAggregateQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GoalAggregateResult(
                Granularity: "quarter",
                Year: 2026,
                Quarter: 1,
                ValorMetaAgregado: 300_000_00L));

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/forecast/aggregate?buId={TestAuthHelper.BuA1}&year=2026&granularity=quarter&quarter=1");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ForecastAggregateBody>();
        body!.ValorMetaAgregado.Should().Be(300_000_00L);
        body.Granularity.Should().Be("quarter");
        body.Quarter.Should().Be(1);
    }

    [Fact]
    public async Task GetAggregate_GranularidadeYear_Retorna200()
    {
        _factory.AggregateHandler
            .Handle(Arg.Any<GetGoalAggregateQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GoalAggregateResult(
                Granularity: "year",
                Year: 2026,
                Quarter: null,
                ValorMetaAgregado: 1_200_000_00L));

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/forecast/aggregate?buId={TestAuthHelper.BuA1}&year=2026&granularity=year");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ForecastAggregateBody>();
        body!.ValorMetaAgregado.Should().Be(1_200_000_00L);
        body.Quarter.Should().BeNull();
    }

    [Fact]
    public async Task GetAggregate_GranularidadeInvalida_Retorna400()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/forecast/aggregate?buId={TestAuthHelper.BuA1}&year=2026&granularity=invalid");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAggregate_QuarterSemNumeroDoTrimestre_Retorna400()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/forecast/aggregate?buId={TestAuthHelper.BuA1}&year=2026&granularity=quarter");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAggregate_ValorMetaAgregadoGrande_SerializadoComoLong()
    {
        const long valorGrande = 9_007_199_254_740_993L;
        _factory.AggregateHandler
            .Handle(Arg.Any<GetGoalAggregateQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GoalAggregateResult("year", 2026, null, valorGrande));

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/forecast/aggregate?buId={TestAuthHelper.BuA1}&year=2026&granularity=year");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();

        raw.Should().Contain("9007199254740993");
        raw.Should().NotContain("9.007199254740993E+15");
    }

    // ── DTOs para desserialização nos testes ─────────────────────────────────

    private sealed record ForecastPanelBody(
        string Scope,
        Guid BuId,
        Guid? OwnerId,
        int Year,
        int Month,
        long? ValorMeta,
        long? Realizado,
        long? PipelineDisponivel,
        long? Gap,
        double? PctAtingimento,
        bool PipelineUnavailable);

    private sealed record ForecastAggregateBody(
        string Granularity,
        int Year,
        int? Quarter,
        long ValorMetaAgregado);
}
