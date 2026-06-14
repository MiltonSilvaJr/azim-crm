using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using GoalForecast.Application.Ports;
using GoalForecast.Application.Queries;
using GoalForecast.Domain.Aggregates;
using GoalForecast.Domain.Authorization;
using GoalForecast.Domain.ValueObjects;

namespace GoalForecast.Application.Tests.Queries;

/// <summary>
/// Testes do <see cref="GetForecastPanelQueryHandler"/>.
/// Inclui PBT-03 (gap exato) e PBT-04 (totalidade do painel).
/// Mapeia: TASK-12, Req 5, Req 6, Req 8, PBT-03, PBT-04, DD-006, DD-007.
/// </summary>
public sealed class GetForecastPanelQueryHandlerTests
{
    private readonly IGoalRepository _repository;
    private readonly IPipelineForecastReader _pipelineReader;
    private readonly GetForecastPanelQueryHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BuId = Guid.NewGuid();

    private static readonly GoalPrincipal Principal = new(
        TenantId: TenantId,
        UserId: Guid.NewGuid(),
        Role: GoalRole.TenantAdmin,
        BuId: null);

    public GetForecastPanelQueryHandlerTests()
    {
        _repository = Substitute.For<IGoalRepository>();
        _pipelineReader = Substitute.For<IPipelineForecastReader>();
        _handler = new GetForecastPanelQueryHandler(_repository, _pipelineReader);
    }

    // --- Cenário 1: com meta + pipeline disponível ---

    [Fact]
    public async Task Handle_com_meta_e_pipeline_deve_calcular_gap_e_pct()
    {
        // Arrange
        var goal = CriarGoal(BuId, null, 2026, 6, 50_000L);
        ConfigurarRepository(goal);
        ConfigurarPipeline(wonTotal: 32_000L, forecastPonderado: 41_000L, available: true);

        // Act
        var result = await ExecutarQuery();

        // Assert
        result.ValorMeta.Should().Be(50_000L);
        result.Realizado.Should().Be(32_000L);
        result.PipelineDisponivel.Should().Be(41_000L);
        result.Gap.Should().Be(18_000L); // 50_000 - 32_000
        result.PctAtingimento.Should().BeApproximately(0.64, 0.001); // 32_000 / 50_000
        result.PipelineUnavailable.Should().BeFalse();
    }

    // --- Cenário 2: sem meta → valorMeta/gap/pct nulos (DD-006, Req 6) ---

    [Fact]
    public async Task Handle_sem_meta_deve_retornar_campos_nulos_e_sem_404()
    {
        // Arrange
        ConfigurarRepository(null);
        ConfigurarPipeline(wonTotal: 32_000L, forecastPonderado: 41_000L, available: true);

        // Act
        var result = await ExecutarQuery();

        // Assert
        result.ValorMeta.Should().BeNull();
        result.Gap.Should().BeNull();
        result.PctAtingimento.Should().BeNull();
        result.Realizado.Should().Be(32_000L);
        result.PipelineDisponivel.Should().Be(41_000L);
        result.PipelineUnavailable.Should().BeFalse();
    }

    // --- Cenário 3: pipeline indisponível → pipelineUnavailable=true (DD-007) ---

    [Fact]
    public async Task Handle_pipeline_indisponivel_deve_setar_pipelineUnavailable_sem_excecao()
    {
        // Arrange
        var goal = CriarGoal(BuId, null, 2026, 6, 50_000L);
        ConfigurarRepository(goal);
        ConfigurarPipeline(wonTotal: 0L, forecastPonderado: 0L, available: false);

        // Act
        var result = await ExecutarQuery();

        // Assert
        result.PipelineUnavailable.Should().BeTrue();
        result.Realizado.Should().BeNull();
        result.PipelineDisponivel.Should().BeNull();
        result.Gap.Should().BeNull();
        result.PctAtingimento.Should().BeNull();
        // ValorMeta presente pois meta existe
        result.ValorMeta.Should().Be(50_000L);
    }

    // --- Cenário 4: valorMeta=0 → pct ausente (design §5.2) ---

    [Fact]
    public async Task Handle_com_valorMeta_zero_deve_omitir_pct()
    {
        // Arrange
        var goal = CriarGoal(BuId, null, 2026, 6, 0L);
        ConfigurarRepository(goal);
        ConfigurarPipeline(wonTotal: 0L, forecastPonderado: 0L, available: true);

        // Act
        var result = await ExecutarQuery();

        // Assert
        result.ValorMeta.Should().Be(0L);
        result.PctAtingimento.Should().BeNull();
    }

    // --- Cenário 5: realizado = valorMeta → gap=0, pct=1 (PBT-03 case) ---

    [Fact]
    public async Task Handle_quando_realizado_igual_valorMeta_gap_deve_ser_zero_e_pct_um()
    {
        // Arrange
        var goal = CriarGoal(BuId, null, 2026, 6, 50_000L);
        ConfigurarRepository(goal);
        ConfigurarPipeline(wonTotal: 50_000L, forecastPonderado: 50_000L, available: true);

        // Act
        var result = await ExecutarQuery();

        // Assert
        result.Gap.Should().Be(0L);
        result.PctAtingimento.Should().BeApproximately(1.0, 0.001);
    }

    // --- PBT-03: gap = valorMeta − realizado exato ---

    [Property(MaxTest = 300)]
    public Property PBT03_gap_deve_ser_exatamente_valorMeta_menos_realizado()
    {
        return Prop.ForAll(
            ArbMap.Default.ArbFor<PositiveInt>(),
            ArbMap.Default.ArbFor<PositiveInt>(),
            (valorMetaPos, realizadoPos) =>
            {
                var repo = Substitute.For<IGoalRepository>();
                var pipeline = Substitute.For<IPipelineForecastReader>();
                var handler = new GetForecastPanelQueryHandler(repo, pipeline);

                var valorMeta = (long)valorMetaPos.Get;
                var realizado = (long)realizadoPos.Get;

                var goal = CriarGoal(BuId, null, 2026, 6, valorMeta);
                repo.FindByKey(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(),
                        Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Goal?>(goal));
                pipeline.Read(Arg.Any<ForecastViewQuery>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(new ForecastViewResult(realizado, realizado, true)));

                var result = handler.Handle(
                    new GetForecastPanelQuery
                    {
                        Principal = Principal,
                        BuId = BuId,
                        Year = 2026,
                        Month = 6
                    },
                    CancellationToken.None).GetAwaiter().GetResult();

                var expectedGap = valorMeta - realizado;
                return result.Gap == expectedGap;
            });
    }

    // --- PBT-04: totalidade — toda combinação válida retorna resultado bem-formado ---

    /// <summary>
    /// Combinação composta (hasMeta × pipelineAvailable × valorMeta × realizado).
    /// FsCheck ForAll aceita máximo 4 parâmetros; combinamos hasMeta e pipelineAvailable
    /// num único tipo auxiliar.
    /// </summary>
    private sealed record PanelScenario(bool HasMeta, bool PipelineAvailable);

    [Property(MaxTest = 200)]
    public Property PBT04_toda_combinacao_valida_retorna_resultado_bem_formado()
    {
        var arbScenario = Arb.From(
            Gen.Elements(
                new PanelScenario(true, true),
                new PanelScenario(true, false),
                new PanelScenario(false, true),
                new PanelScenario(false, false)));

        return Prop.ForAll(
            arbScenario,
            ArbMap.Default.ArbFor<PositiveInt>(),
            ArbMap.Default.ArbFor<PositiveInt>(),
            (scenario, valorMetaPos, realizadoPos) =>
            {
                var repo = Substitute.For<IGoalRepository>();
                var pipeline = Substitute.For<IPipelineForecastReader>();
                var handler = new GetForecastPanelQueryHandler(repo, pipeline);

                var valorMeta = (long)valorMetaPos.Get;
                var realizado = (long)realizadoPos.Get;

                Goal? goal = scenario.HasMeta ? CriarGoal(BuId, null, 2026, 6, valorMeta) : null;

                // NSubstitute: usa ReturnsForAnyArgs para Goal? (nullable)
                repo.FindByKey(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(),
                        Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Goal?>(goal));

                pipeline.Read(Arg.Any<ForecastViewQuery>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(scenario.PipelineAvailable
                        ? new ForecastViewResult(realizado, realizado, true)
                        : ForecastViewResult.Unavailable));

                ForecastPanelResult result;
                try
                {
                    result = handler.Handle(
                        new GetForecastPanelQuery
                        {
                            Principal = Principal,
                            BuId = BuId,
                            Year = 2026,
                            Month = 6
                        },
                        CancellationToken.None).GetAwaiter().GetResult();
                }
                catch
                {
                    // Nenhuma exceção deve ser propagada (PBT-04).
                    return false;
                }

                // Sem NaN no pct (PBT-04).
                if (result.PctAtingimento.HasValue && double.IsNaN(result.PctAtingimento.Value))
                    return false;

                // Sem meta: valorMeta/gap/pct devem ser nulos (DD-006).
                if (!scenario.HasMeta)
                {
                    if (result.ValorMeta.HasValue) return false;
                    if (result.Gap.HasValue) return false;
                    if (result.PctAtingimento.HasValue) return false;
                }

                // Pipeline indisponível: realizado/pipelineDisponivel nulos (DD-007).
                if (!scenario.PipelineAvailable)
                {
                    if (!result.PipelineUnavailable) return false;
                    if (result.Realizado.HasValue) return false;
                    if (result.PipelineDisponivel.HasValue) return false;
                }

                return true;
            });
    }

    // --- Helpers ---

    private void ConfigurarRepository(Goal? goal) =>
        _repository.FindByKey(TenantId, BuId, null, 2026, 6, default)
            .Returns(Task.FromResult(goal));

    private void ConfigurarPipeline(long wonTotal, long forecastPonderado, bool available) =>
        _pipelineReader.Read(Arg.Any<ForecastViewQuery>(), default)
            .Returns(Task.FromResult(new ForecastViewResult(wonTotal, forecastPonderado, available)));

    private Task<ForecastPanelResult> ExecutarQuery() =>
        _handler.Handle(
            new GetForecastPanelQuery
            {
                Principal = Principal,
                BuId = BuId,
                Year = 2026,
                Month = 6
            },
            CancellationToken.None);

    private static Goal CriarGoal(Guid buId, Guid? ownerId, int year, int month, long cents)
    {
        var scope = ownerId.HasValue
            ? GoalScope.ForResponsavel(buId, ownerId.Value)
            : GoalScope.ForBu(buId);
        return Goal.Create(TenantId, scope, new GoalPeriod(year, month), Money.Of(cents));
    }
}
