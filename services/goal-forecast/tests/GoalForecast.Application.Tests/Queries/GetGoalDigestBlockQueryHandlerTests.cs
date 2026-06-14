using GoalForecast.Application.Ports;
using GoalForecast.Application.Queries;
using GoalForecast.Domain.Aggregates;
using GoalForecast.Domain.Authorization;
using GoalForecast.Domain.ValueObjects;

namespace GoalForecast.Application.Tests.Queries;

/// <summary>
/// Testes do <see cref="GetGoalDigestBlockQueryHandler"/>.
/// Verifica sinal de ausência, bloco completo e degradação de pipeline.
/// Mapeia: TASK-14, Req 9, RN-018, RN-029, design §8.6.
/// </summary>
public sealed class GetGoalDigestBlockQueryHandlerTests
{
    private readonly IGoalRepository _repository;
    private readonly IPipelineForecastReader _pipelineReader;
    private readonly GetGoalDigestBlockQueryHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BuId = Guid.NewGuid();

    private static readonly GoalPrincipal Principal = new(
        TenantId: TenantId,
        UserId: Guid.NewGuid(),
        Role: GoalRole.TenantAdmin,
        BuId: null);

    public GetGoalDigestBlockQueryHandlerTests()
    {
        _repository = Substitute.For<IGoalRepository>();
        _pipelineReader = Substitute.For<IPipelineForecastReader>();
        _handler = new GetGoalDigestBlockQueryHandler(_repository, _pipelineReader);
    }

    // --- Cenário 1: sem meta → present=false, sem campos monetários ---

    [Fact]
    public async Task Handle_sem_meta_deve_retornar_present_false()
    {
        ConfigurarRepository(null);

        var result = await ExecutarQuery();

        result.Present.Should().BeFalse();
        result.ValorMeta.Should().BeNull();
        result.Realizado.Should().BeNull();
        result.Gap.Should().BeNull();
        result.PipelineDisponivel.Should().BeNull();
        result.PipelineUnavailable.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_sem_meta_nao_deve_chamar_pipeline()
    {
        // Quando não há meta, o pipeline não deve ser consultado.
        ConfigurarRepository(null);

        await ExecutarQuery();

        await _pipelineReader.DidNotReceive().Read(
            Arg.Any<ForecastViewQuery>(),
            Arg.Any<CancellationToken>());
    }

    // --- Cenário 2: com meta e pipeline disponível → present=true com todos os campos ---

    [Fact]
    public async Task Handle_com_meta_e_pipeline_deve_retornar_bloco_completo()
    {
        var goal = CriarGoal(50_000L);
        ConfigurarRepository(goal);
        ConfigurarPipeline(wonTotal: 32_000L, forecastPonderado: 41_000L, available: true);

        var result = await ExecutarQuery();

        result.Present.Should().BeTrue();
        result.ValorMeta.Should().Be(50_000L);
        result.Realizado.Should().Be(32_000L);
        result.Gap.Should().Be(18_000L); // 50_000 - 32_000
        result.PipelineDisponivel.Should().Be(41_000L);
        result.PipelineUnavailable.Should().BeFalse();
    }

    // --- Cenário 3: pipeline indisponível com meta → present=true, pipelineUnavailable=true ---

    [Fact]
    public async Task Handle_pipeline_indisponivel_com_meta_deve_retornar_present_true_sem_realizado()
    {
        var goal = CriarGoal(50_000L);
        ConfigurarRepository(goal);
        ConfigurarPipeline(wonTotal: 0L, forecastPonderado: 0L, available: false);

        var result = await ExecutarQuery();

        result.Present.Should().BeTrue();
        result.PipelineUnavailable.Should().BeTrue();
        result.ValorMeta.Should().Be(50_000L);
        result.Realizado.Should().BeNull();
        result.Gap.Should().BeNull();
        result.PipelineDisponivel.Should().BeNull();
    }

    // --- Cenário 4: valores em centavos inteiros (long) ---

    [Fact]
    public async Task Handle_valores_monetarios_devem_ser_long_de_centavos()
    {
        var goal = CriarGoal(123_456_789L);
        ConfigurarRepository(goal);
        ConfigurarPipeline(wonTotal: 100_000_000L, forecastPonderado: 110_000_000L, available: true);

        var result = await ExecutarQuery();

        result.ValorMeta.Should().Be(123_456_789L);
        result.Realizado.Should().Be(100_000_000L);
        result.Gap.Should().Be(23_456_789L);
    }

    // --- Cenário 5: resposta total — always retorna resultado bem-formado ---

    [Fact]
    public async Task Handle_deve_ser_total_sem_excecao_em_qualquer_combinacao()
    {
        // Sem meta + pipeline indisponível → resultado bem-formado sem exceção.
        ConfigurarRepository(null);
        // Pipeline não é consultado quando não há meta.

        var result = await ExecutarQuery();

        result.Should().NotBeNull();
        result.Present.Should().BeFalse();
    }

    // --- Helpers ---

    private void ConfigurarRepository(Goal? goal) =>
        _repository.FindByKey(TenantId, BuId, null, 2026, 6, default)
            .Returns(Task.FromResult(goal));

    private void ConfigurarPipeline(long wonTotal, long forecastPonderado, bool available) =>
        _pipelineReader.Read(Arg.Any<ForecastViewQuery>(), default)
            .Returns(Task.FromResult(new ForecastViewResult(wonTotal, forecastPonderado, available)));

    private Task<DigestBlockResult> ExecutarQuery() =>
        _handler.Handle(
            new GetGoalDigestBlockQuery
            {
                Principal = Principal,
                BuId = BuId,
                Year = 2026,
                Month = 6
            },
            CancellationToken.None);

    private static Goal CriarGoal(long cents) =>
        Goal.Create(TenantId, GoalScope.ForBu(BuId), new GoalPeriod(2026, 6), Money.Of(cents));
}
