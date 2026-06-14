using System.Net;
using System.Net.Http.Json;
using GoalForecast.Api.Tests.Infrastructure;
using GoalForecast.Application.Queries;
using NSubstitute;

namespace GoalForecast.Api.Tests.Controllers;

/// <summary>
/// Testes de API para GET /api/v1/internal/goals/digest-block.
/// Verifica autenticação de serviço (ServiceScope), sinal present=false, campos monetários como long.
///
/// Mapeia: TASK-25, design §8.6, §10, Req 9, RN-018, RN-029.
/// </summary>
public sealed class InternalGoalsEndpointsTests : IClassFixture<GoalForecastWebFactory>
{
    private readonly GoalForecastWebFactory _factory;
    private readonly HttpClient _client;

    public InternalGoalsEndpointsTests(GoalForecastWebFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Autenticação de serviço ───────────────────────────────────────────────

    [Fact]
    public async Task GetDigestBlock_SemAuth_Retorna401()
    {
        var response = await _client.GetAsync(
            $"/api/v1/internal/goals/digest-block?buId={TestAuthHelper.BuA1}&year=2026&month=1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDigestBlock_SemServiceScope_Retorna403()
    {
        // TenantAdmin não é ServiceScope — deve ser recusado
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/internal/goals/digest-block?buId={TestAuthHelper.BuA1}&year=2026&month=1");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetDigestBlock_Vendedor_Retorna403()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/internal/goals/digest-block?buId={TestAuthHelper.BuA1}&year=2026&month=1");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.VendedorA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Meta presente ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDigestBlock_ComMeta_Retorna200ComPresentTrue()
    {
        _factory.DigestBlockHandler
            .Handle(Arg.Any<GetGoalDigestBlockQuery>(), Arg.Any<CancellationToken>())
            .Returns(DigestBlockResult.WithMeta(
                valorMeta: 100_000_00L,
                realizado: 60_000_00L,
                gap: -40_000_00L,
                pipelineDisponivel: 40_000_00L,
                pipelineUnavailable: false));

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/internal/goals/digest-block?buId={TestAuthHelper.BuA1}&year=2026&month=1");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.ServiceScope);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DigestBlockBody>();
        body!.Present.Should().BeTrue();
        body.ValorMeta.Should().Be(100_000_00L);
        body.Realizado.Should().Be(60_000_00L);
        body.Gap.Should().Be(-40_000_00L);
        body.PipelineDisponivel.Should().Be(40_000_00L);
        body.PipelineUnavailable.Should().BeFalse();
    }

    // ── Meta ausente: sinal present=false ────────────────────────────────────

    [Fact]
    public async Task GetDigestBlock_SemMeta_Retorna200ComPresentFalseESemCamposMonetarios()
    {
        _factory.DigestBlockHandler
            .Handle(Arg.Any<GetGoalDigestBlockQuery>(), Arg.Any<CancellationToken>())
            .Returns(DigestBlockResult.Absent());

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/internal/goals/digest-block?buId={TestAuthHelper.BuA1}&year=2026&month=2");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.ServiceScope);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DigestBlockBody>();
        body!.Present.Should().BeFalse();
        body.ValorMeta.Should().BeNull();
        body.Realizado.Should().BeNull();
        body.Gap.Should().BeNull();
        body.PipelineDisponivel.Should().BeNull();
    }

    // ── Degradação: pipeline indisponível com meta ────────────────────────────

    [Fact]
    public async Task GetDigestBlock_PipelineIndisponivel_Retorna200ComPipelineUnavailableTrue()
    {
        _factory.DigestBlockHandler
            .Handle(Arg.Any<GetGoalDigestBlockQuery>(), Arg.Any<CancellationToken>())
            .Returns(DigestBlockResult.WithMeta(
                valorMeta: 50_000_00L,
                realizado: null,
                gap: null,
                pipelineDisponivel: null,
                pipelineUnavailable: true));

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/internal/goals/digest-block?buId={TestAuthHelper.BuA1}&year=2026&month=3");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.ServiceScope);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DigestBlockBody>();
        body!.Present.Should().BeTrue();
        body.PipelineUnavailable.Should().BeTrue();
        body.ValorMeta.Should().Be(50_000_00L);
        body.Realizado.Should().BeNull();
    }

    // ── Integridade monetária ─────────────────────────────────────────────────

    [Fact]
    public async Task GetDigestBlock_ValorMetaGrande_SerializadoComoLong()
    {
        const long valorGrande = 9_007_199_254_740_993L;
        _factory.DigestBlockHandler
            .Handle(Arg.Any<GetGoalDigestBlockQuery>(), Arg.Any<CancellationToken>())
            .Returns(DigestBlockResult.WithMeta(valorGrande, null, null, null, true));

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/internal/goals/digest-block?buId={TestAuthHelper.BuA1}&year=2026&month=4");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.ServiceScope);

        var response = await _client.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();

        raw.Should().Contain("9007199254740993");
        raw.Should().NotContain("9.007199254740993E+15");
    }

    // ── Parâmetros obrigatórios ───────────────────────────────────────────────

    [Fact]
    public async Task GetDigestBlock_SemBuId_Retorna400()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "/api/v1/internal/goals/digest-block?year=2026&month=1");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.ServiceScope);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── DTO para desserialização nos testes ───────────────────────────────────

    private sealed record DigestBlockBody(
        bool Present,
        long? ValorMeta,
        long? Realizado,
        long? Gap,
        long? PipelineDisponivel,
        bool PipelineUnavailable);
}
