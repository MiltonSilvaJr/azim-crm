using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using GoalForecast.Application.Ports;
using GoalForecast.Application.Queries;
using GoalForecast.Domain.Aggregates;
using GoalForecast.Domain.Authorization;
using GoalForecast.Domain.ValueObjects;
using AppException = GoalForecast.Application.Common.ApplicationException;

namespace GoalForecast.Application.Tests.Queries;

/// <summary>
/// Testes do <see cref="GetGoalAggregateQueryHandler"/>.
/// PBT-02: agregação exata, meses ausentes = zero, sem arredondamento.
/// Mapeia: TASK-13, Req 7, PBT-02, DD-003, RN-027.
/// </summary>
public sealed class GetGoalAggregateQueryHandlerTests
{
    private readonly IGoalRepository _repository;
    private readonly GetGoalAggregateQueryHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BuId = Guid.NewGuid();

    private static readonly GoalPrincipal Principal = new(
        TenantId: TenantId,
        UserId: Guid.NewGuid(),
        Role: GoalRole.TenantAdmin,
        BuId: null);

    public GetGoalAggregateQueryHandlerTests()
    {
        _repository = Substitute.For<IGoalRepository>();
        _handler = new GetGoalAggregateQueryHandler(_repository);
    }

    // --- Cenário 1: trimestre com 3 meses presentes ---

    [Fact]
    public async Task Handle_trimestre_com_3_meses_deve_somar_corretamente()
    {
        // Q1 2026 = meses 1, 2, 3
        var goals = new List<Goal>
        {
            CriarGoal(1, 10_000L),
            CriarGoal(2, 20_000L),
            CriarGoal(3, 30_000L)
        };
        _repository.ListByYear(TenantId, BuId, null, 2026, default).Returns(goals);

        var result = await _handler.Handle(
            BuildQuery(AggregateGranularity.Quarter, quarter: 1), CancellationToken.None);

        result.ValorMetaAgregado.Should().Be(60_000L);
        result.Quarter.Should().Be(1);
        result.Granularity.Should().Be("quarter");
    }

    // --- Cenário 2: trimestre com 1 mês ausente (ausente = 0) ---

    [Fact]
    public async Task Handle_trimestre_com_mes_ausente_deve_contribuir_zero()
    {
        // Q2 2026 = meses 4, 5, 6 — só mês 5 presente
        var goals = new List<Goal> { CriarGoal(5, 15_000L) };
        _repository.ListByYear(TenantId, BuId, null, 2026, default).Returns(goals);

        var result = await _handler.Handle(
            BuildQuery(AggregateGranularity.Quarter, quarter: 2), CancellationToken.None);

        result.ValorMetaAgregado.Should().Be(15_000L);
    }

    // --- Cenário 3: ano completo (12 meses) ---

    [Fact]
    public async Task Handle_ano_completo_deve_somar_12_meses()
    {
        var goals = Enumerable.Range(1, 12)
            .Select(m => CriarGoal(m, 10_000L))
            .ToList();
        _repository.ListByYear(TenantId, BuId, null, 2026, default).Returns(goals);

        var result = await _handler.Handle(
            BuildQuery(AggregateGranularity.Year), CancellationToken.None);

        result.ValorMetaAgregado.Should().Be(120_000L);
        result.Quarter.Should().BeNull();
        result.Granularity.Should().Be("year");
    }

    // --- Cenário 4: ano parcial (3 meses) ---

    [Fact]
    public async Task Handle_ano_parcial_deve_somar_apenas_meses_presentes()
    {
        var goals = new List<Goal>
        {
            CriarGoal(1, 5_000L),
            CriarGoal(6, 7_000L),
            CriarGoal(12, 3_000L)
        };
        _repository.ListByYear(TenantId, BuId, null, 2026, default).Returns(goals);

        var result = await _handler.Handle(
            BuildQuery(AggregateGranularity.Year), CancellationToken.None);

        result.ValorMetaAgregado.Should().Be(15_000L);
    }

    // --- Cenário 5: Quarter inválido retorna erro ---

    [Fact]
    public async Task Handle_quarter_invalido_deve_lancar_erro_400()
    {
        _repository.ListByYear(TenantId, BuId, null, 2026, default).Returns([]);

        var act = async () => await _handler.Handle(
            BuildQuery(AggregateGranularity.Quarter, quarter: 5),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.SuggestedHttpStatus.Should().Be(400);
    }

    // --- PBT-02: soma exata em long, meses ausentes = zero ---

    [Property(MaxTest = 300)]
    public Property PBT02_soma_trimestral_exata_em_long()
    {
        return Prop.ForAll(
            Arb.From(Gen.Elements(1, 2, 3, 4)),          // quarter
            ArbMap.Default.ArbFor<PositiveInt[]>()
                .Filter(a => a.Length > 0 && a.Length <= 3), // até 3 meses presentes no trimestre
            (quarter, posInts) =>
            {
                var repo = Substitute.For<IGoalRepository>();
                var handler = new GetGoalAggregateQueryHandler(repo);

                var firstMonth = (quarter - 1) * 3 + 1;
                var months = Enumerable.Range(0, posInts.Length)
                    .Select(i => firstMonth + i)
                    .ToArray();
                var values = posInts.Select(p => (long)p.Get).ToArray();

                var goals = months.Zip(values, (m, v) => CriarGoal(m, v)).ToList();
                repo.ListByYear(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(),
                        Arg.Any<int>(), Arg.Any<CancellationToken>())
                    .Returns(goals);

                var result = handler.Handle(
                    new GetGoalAggregateQuery
                    {
                        Principal = Principal,
                        BuId = BuId,
                        Year = 2026,
                        Granularity = AggregateGranularity.Quarter,
                        Quarter = quarter
                    },
                    CancellationToken.None).GetAwaiter().GetResult();

                var expectedTotal = values.Sum();
                return result.ValorMetaAgregado == expectedTotal;
            });
    }

    [Property(MaxTest = 200)]
    public Property PBT02_soma_anual_exata_em_long()
    {
        return Prop.ForAll(
            ArbMap.Default.ArbFor<PositiveInt[]>()
                .Filter(a => a.Length > 0 && a.Length <= 12),
            (posInts) =>
            {
                var repo = Substitute.For<IGoalRepository>();
                var handler = new GetGoalAggregateQueryHandler(repo);

                var values = posInts.Select(p => (long)p.Get).ToArray();
                var goals = Enumerable.Range(0, values.Length)
                    .Select(i => CriarGoal(i + 1, values[i]))
                    .ToList();

                repo.ListByYear(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(),
                        Arg.Any<int>(), Arg.Any<CancellationToken>())
                    .Returns(goals);

                var result = handler.Handle(
                    new GetGoalAggregateQuery
                    {
                        Principal = Principal,
                        BuId = BuId,
                        Year = 2026,
                        Granularity = AggregateGranularity.Year
                    },
                    CancellationToken.None).GetAwaiter().GetResult();

                var expectedTotal = values.Sum();
                return result.ValorMetaAgregado == expectedTotal;
            });
    }

    // --- Helpers ---

    private GetGoalAggregateQuery BuildQuery(AggregateGranularity granularity, int? quarter = null) =>
        new()
        {
            Principal = Principal,
            BuId = BuId,
            Year = 2026,
            Granularity = granularity,
            Quarter = quarter
        };

    private static Goal CriarGoal(int month, long cents) =>
        Goal.Create(TenantId, GoalScope.ForBu(BuId), new GoalPeriod(2026, month), Money.Of(cents));
}
