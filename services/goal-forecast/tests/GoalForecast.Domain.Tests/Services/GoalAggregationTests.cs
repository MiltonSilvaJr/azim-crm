using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using GoalForecast.Domain.Aggregates;
using GoalForecast.Domain.Exceptions;
using GoalForecast.Domain.Services;
using GoalForecast.Domain.ValueObjects;
using Xunit;

namespace GoalForecast.Domain.Tests.Services;

/// <summary>
/// Testes do serviço de domínio puro <see cref="GoalAggregation"/>.
/// Cobre TASK-06: SumByQuarter, SumByYear, meses ausentes = zero e PBT-02.
/// Mapeia: Req 7, PBT-02, RN-027, design §4.6, TASK-06.
/// </summary>
public sealed class GoalAggregationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly GoalScope ScopeBu = GoalScope.ForBu(Guid.NewGuid());

    // =========================================================================
    // Helpers
    // =========================================================================

    private static Goal CreateGoal(int year, int month, long cents) =>
        Goal.Create(TenantId, ScopeBu, new GoalPeriod(year, month), Money.Of(cents));

    // =========================================================================
    // GoalUpdated: campos canônicos (TASK-06 ST-01)
    // =========================================================================

    [Fact(DisplayName = "GoalUpdated tem todos os campos canônicos do design §4.4")]
    public void GoalUpdated_HasAllCanonicalFields()
    {
        var goal = CreateGoal(2026, 6, 50_000_00L);
        var evt = (GoalForecast.Domain.Events.GoalUpdated)goal.DomainEvents[0];

        evt.EventId.Should().NotBe(Guid.Empty);
        evt.GoalId.Should().Be(goal.Id);
        evt.TenantId.Should().Be(TenantId);
        evt.BuId.Should().Be(ScopeBu.BuId);
        evt.OwnerId.Should().BeNull(); // escopo BU
        evt.Year.Should().Be(2026);
        evt.Month.Should().Be(6);
        evt.Action.Should().Be(GoalForecast.Domain.Events.GoalUpdatedAction.Created);
        evt.ValorMetaAnterior.Should().BeNull(); // criação
        evt.ValorMetaNovo.Should().Be(50_000_00L);
        evt.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "GoalUpdated action=created tem valorMetaAnterior nulo")]
    public void GoalUpdated_Created_ValorMetaAnteriorIsNull()
    {
        var goal = CreateGoal(2026, 1, 1000L);
        var evt = (GoalForecast.Domain.Events.GoalUpdated)goal.DomainEvents[0];

        evt.ValorMetaAnterior.Should().BeNull();
        evt.Action.Should().Be(GoalForecast.Domain.Events.GoalUpdatedAction.Created);
    }

    [Fact(DisplayName = "GoalUpdated action=updated tem valorMetaAnterior com valor anterior")]
    public void GoalUpdated_Updated_ValorMetaAnteriorIsNotNull()
    {
        var goal = CreateGoal(2026, 1, 1000L);
        goal.ClearDomainEvents();
        goal.ChangeValorMeta(Money.Of(2000L));

        var evt = (GoalForecast.Domain.Events.GoalUpdated)goal.DomainEvents[0];

        evt.ValorMetaAnterior.Should().Be(1000L);
        evt.ValorMetaNovo.Should().Be(2000L);
        evt.Action.Should().Be(GoalForecast.Domain.Events.GoalUpdatedAction.Updated);
    }

    // =========================================================================
    // SumByQuarter — casos determinísticos
    // =========================================================================

    [Fact(DisplayName = "SumByQuarter com 3 meses do trimestre retorna soma exata")]
    public void SumByQuarter_ThreeMonths_ReturnsSumExact()
    {
        var goals = new[]
        {
            CreateGoal(2026, 1, 1000L),
            CreateGoal(2026, 2, 2000L),
            CreateGoal(2026, 3, 3000L)
        };
        var period = new GoalPeriod(2026, 1); // Q1

        var result = GoalAggregation.SumByQuarter(goals, period);

        result.Cents.Should().Be(6000L);
    }

    [Fact(DisplayName = "SumByQuarter com mês ausente contribui com zero")]
    public void SumByQuarter_MissingMonth_ContributesZero()
    {
        var goals = new[]
        {
            CreateGoal(2026, 1, 1000L),
            // mês 2 ausente
            CreateGoal(2026, 3, 3000L)
        };
        var period = new GoalPeriod(2026, 1); // Q1

        var result = GoalAggregation.SumByQuarter(goals, period);

        result.Cents.Should().Be(4000L);
    }

    [Fact(DisplayName = "SumByQuarter com todos os meses ausentes retorna Money.Zero")]
    public void SumByQuarter_AllMissing_ReturnsZero()
    {
        var goals = Array.Empty<Goal>();
        var period = new GoalPeriod(2026, 1);

        var result = GoalAggregation.SumByQuarter(goals, period);

        result.Should().Be(Money.Zero);
    }

    [Fact(DisplayName = "SumByQuarter ignora goals de trimestres diferentes")]
    public void SumByQuarter_OtherQuarterGoals_AreIgnored()
    {
        var goals = new[]
        {
            CreateGoal(2026, 1, 1000L), // Q1
            CreateGoal(2026, 4, 9999L), // Q2 — deve ser ignorado
            CreateGoal(2026, 3, 3000L)  // Q1
        };
        var period = new GoalPeriod(2026, 1); // Q1

        var result = GoalAggregation.SumByQuarter(goals, period);

        result.Cents.Should().Be(4000L);
    }

    // =========================================================================
    // SumByYear — casos determinísticos
    // =========================================================================

    [Fact(DisplayName = "SumByYear com 12 meses retorna soma exata")]
    public void SumByYear_TwelveMonths_ReturnsSumExact()
    {
        var goals = Enumerable.Range(1, 12)
            .Select(m => CreateGoal(2026, m, 1000L))
            .ToArray();

        var result = GoalAggregation.SumByYear(goals, 2026);

        result.Cents.Should().Be(12_000L);
    }

    [Fact(DisplayName = "SumByYear com meses ausentes contribui com zero para ausentes")]
    public void SumByYear_SomeMonthsMissing_ContributesZeroForMissing()
    {
        var goals = new[]
        {
            CreateGoal(2026, 1, 5000L),
            CreateGoal(2026, 12, 3000L)
            // 10 meses ausentes
        };

        var result = GoalAggregation.SumByYear(goals, 2026);

        result.Cents.Should().Be(8000L);
    }

    [Fact(DisplayName = "SumByYear ignora goals de ano diferente")]
    public void SumByYear_OtherYearGoals_AreIgnored()
    {
        var goals = new[]
        {
            CreateGoal(2026, 1, 1000L),
            CreateGoal(2025, 1, 9999L) // ano diferente
        };

        var result = GoalAggregation.SumByYear(goals, 2026);

        result.Cents.Should().Be(1000L);
    }

    [Fact(DisplayName = "SumByYear sem goals retorna Money.Zero")]
    public void SumByYear_Empty_ReturnsZero()
    {
        var result = GoalAggregation.SumByYear(Array.Empty<Goal>(), 2026);

        result.Should().Be(Money.Zero);
    }

    // =========================================================================
    // ADR-0008: Multimoeda — proibição de mix de moedas
    // =========================================================================

    [Fact(DisplayName = "ADR-0008: SumByQuarter com goals de moedas mistas lança DomainException")]
    public void SumByQuarter_MixedCurrencies_ThrowsDomainException()
    {
        var goals = new[]
        {
            Goal.Create(TenantId, ScopeBu, new GoalPeriod(2026, 1), Money.Of(1000L, "BRL")),
            Goal.Create(TenantId, ScopeBu, new GoalPeriod(2026, 2), Money.Of(1000L, "USD"))
        };
        var period = new GoalPeriod(2026, 1); // Q1

        // currency esperada = "BRL", mas existe um goal em "USD" → mismatch
        var act = () => GoalAggregation.SumByQuarter(goals, period, "BRL");

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-001");
    }

    [Fact(DisplayName = "ADR-0008: SumByYear com goals de moedas mistas lança DomainException")]
    public void SumByYear_MixedCurrencies_ThrowsDomainException()
    {
        var goals = new[]
        {
            Goal.Create(TenantId, ScopeBu, new GoalPeriod(2026, 1), Money.Of(1000L, "BRL")),
            Goal.Create(TenantId, ScopeBu, new GoalPeriod(2026, 6), Money.Of(1000L, "EUR"))
        };

        var act = () => GoalAggregation.SumByYear(goals, 2026, "BRL");

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-001");
    }

    [Fact(DisplayName = "ADR-0008: SumByQuarter com goals em USD retorna soma em USD")]
    public void SumByQuarter_UsdGoals_ReturnsSumInUsd()
    {
        var goals = new[]
        {
            Goal.Create(TenantId, ScopeBu, new GoalPeriod(2026, 1), Money.Of(1000L, "USD")),
            Goal.Create(TenantId, ScopeBu, new GoalPeriod(2026, 2), Money.Of(2000L, "USD"))
        };
        var period = new GoalPeriod(2026, 1);

        var result = GoalAggregation.SumByQuarter(goals, period, "USD");

        result.Cents.Should().Be(3000L);
        result.Currency.Should().Be("USD");
    }

    // =========================================================================
    // GoalAggregation é função pura — não tem estado nem I/O
    // =========================================================================

    [Fact(DisplayName = "SumByQuarter é determinística com os mesmos inputs")]
    public void SumByQuarter_IsDeterministic()
    {
        var goals = new[] { CreateGoal(2026, 1, 100L), CreateGoal(2026, 2, 200L) };
        var period = new GoalPeriod(2026, 1);

        var r1 = GoalAggregation.SumByQuarter(goals, period);
        var r2 = GoalAggregation.SumByQuarter(goals, period);

        r1.Should().Be(r2);
    }

    // =========================================================================
    // PBT-02: para qualquer subconjunto aleatório de metas mensais de mesmo escopo,
    // soma trimestral = soma dos 3 meses do trimestre; anual = soma dos 12 meses.
    // Meses ausentes contribuem com zero. Resultado exato em centavos.
    // Mapeia: Req 7, PBT-02, RN-027, tasks.md §1.2.
    // =========================================================================

    /// <summary>
    /// PBT-02 — trimestral: para subconjunto aleatório de meses de um trimestre,
    /// SumByQuarter == soma dos valores presentes (ausentes == 0).
    /// </summary>
    [Property(Arbitrary = new[] { typeof(QuarterGoalSetArbitrary) }, MaxTest = 200,
        DisplayName = "PBT-02: SumByQuarter == soma exata dos meses presentes no trimestre")]
    public Property Pbt02_SumByQuarter_ExactSum(QuarterGoalSet set)
    {
        var result = GoalAggregation.SumByQuarter(set.Goals, set.Period);
        var expected = set.Goals
            .Where(g => g.Period.Quarter() == set.Period.Quarter() && g.Period.Year == set.Period.Year)
            .Sum(g => g.ValorMeta.Cents);

        return Prop.ToProperty(result.Cents == expected);
    }

    /// <summary>
    /// PBT-02 — anual: para subconjunto aleatório de meses de um ano,
    /// SumByYear == soma dos valores presentes (ausentes == 0).
    /// </summary>
    [Property(Arbitrary = new[] { typeof(YearGoalSetArbitrary) }, MaxTest = 200,
        DisplayName = "PBT-02: SumByYear == soma exata dos meses presentes no ano")]
    public Property Pbt02_SumByYear_ExactSum(YearGoalSet set)
    {
        var result = GoalAggregation.SumByYear(set.Goals, set.Year);
        var expected = set.Goals
            .Where(g => g.Period.Year == set.Year)
            .Sum(g => g.ValorMeta.Cents);

        return Prop.ToProperty(result.Cents == expected);
    }

    /// <summary>
    /// PBT-02 — conservação: soma anual >= soma trimestral (nenhum mês conta duplamente).
    /// </summary>
    [Property(Arbitrary = new[] { typeof(YearGoalSetArbitrary) }, MaxTest = 200,
        DisplayName = "PBT-02: Soma anual >= soma de qualquer trimestre do mesmo ano")]
    public Property Pbt02_AnnualSum_GeQuarterlySum(YearGoalSet set)
    {
        var annual = GoalAggregation.SumByYear(set.Goals, set.Year);
        var maxQuarterly = Enumerable.Range(1, 4)
            .Max(q =>
            {
                var refMonth = (q - 1) * 3 + 1;
                var period = new GoalPeriod(set.Year, refMonth);
                return GoalAggregation.SumByQuarter(set.Goals, period).Cents;
            });

        return Prop.ToProperty(annual.Cents >= maxQuarterly);
    }
}

// =============================================================================
// Tipos e geradores para PBT-02
// =============================================================================

/// <summary>
/// Conjunto de metas de um trimestre para PBT-02.
/// Goals é subconjunto (possivelmente vazio) dos meses do trimestre.
/// </summary>
public sealed class QuarterGoalSet
{
    public Goal[] Goals { get; }
    public GoalPeriod Period { get; }

    public QuarterGoalSet(Goal[] goals, GoalPeriod period)
    {
        Goals = goals;
        Period = period;
    }
}

/// <summary>Gerador de QuarterGoalSet para PBT-02.</summary>
public static class QuarterGoalSetArbitrary
{
    private static readonly Guid FixedTenantId = Guid.NewGuid();
    private static readonly GoalScope FixedScope = GoalScope.ForBu(Guid.NewGuid());

    public static Arbitrary<QuarterGoalSet> Generate()
    {
        // Gera um trimestre (1..4) e um subconjunto aleatório dos 3 meses
        var longGen = Gen.Choose(0, 1_000_000_000).Select(v => (long)v);
        var gen = from year in Gen.Choose(2024, 2027)
                  from quarter in Gen.Choose(1, 4)
                  from monthBits in Gen.Choose(0, 7) // 3 bits = subconjunto dos 3 meses
                  from values in Gen.ListOf(longGen, 3)
                  select BuildSet(year, quarter, monthBits, values.ToArray());

        return gen.ToArbitrary();
    }

    private static QuarterGoalSet BuildSet(int year, int quarter, int monthBits, long[] values)
    {
        var firstMonth = (quarter - 1) * 3 + 1;
        var goals = new List<Goal>();

        for (var i = 0; i < 3; i++)
        {
            if ((monthBits & (1 << i)) != 0)
            {
                var month = firstMonth + i;
                goals.Add(Goal.Create(FixedTenantId, FixedScope, new GoalPeriod(year, month), Money.Of(values[i])));
            }
        }

        var refPeriod = new GoalPeriod(year, firstMonth);
        return new QuarterGoalSet(goals.ToArray(), refPeriod);
    }
}

/// <summary>
/// Conjunto de metas de um ano para PBT-02.
/// Goals é subconjunto (possivelmente vazio) dos 12 meses.
/// </summary>
public sealed class YearGoalSet
{
    public Goal[] Goals { get; }
    public int Year { get; }

    public YearGoalSet(Goal[] goals, int year)
    {
        Goals = goals;
        Year = year;
    }
}

/// <summary>Gerador de YearGoalSet para PBT-02.</summary>
public static class YearGoalSetArbitrary
{
    private static readonly Guid FixedTenantId = Guid.NewGuid();
    private static readonly GoalScope FixedScope = GoalScope.ForBu(Guid.NewGuid());

    public static Arbitrary<YearGoalSet> Generate()
    {
        // Gera um ano e um subconjunto aleatório de 12 meses (12 bits = 4096 combinações)
        var longGen = Gen.Choose(0, 100_000_000).Select(v => (long)v);
        var gen = from year in Gen.Choose(2024, 2027)
                  from monthBits in Gen.Choose(0, 4095) // 12 bits
                  from values in Gen.ListOf(longGen, 12)
                  select BuildSet(year, monthBits, values.ToArray());

        return gen.ToArbitrary();
    }

    private static YearGoalSet BuildSet(int year, int monthBits, long[] values)
    {
        var goals = new List<Goal>();

        for (var m = 0; m < 12; m++)
        {
            if ((monthBits & (1 << m)) != 0)
            {
                var month = m + 1;
                goals.Add(Goal.Create(FixedTenantId, FixedScope, new GoalPeriod(year, month), Money.Of(values[m])));
            }
        }

        return new YearGoalSet(goals.ToArray(), year);
    }
}
