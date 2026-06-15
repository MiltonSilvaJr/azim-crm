using OpportunityPipeline.Domain.Opportunities.Services;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Tests.Opportunities.Services;

/// <summary>
/// Testes para ForecastCalculator, CommissionCalculator, NetForecastCalculator,
/// ExpectedCloseDatePolicy, StagnationSpecification, OverdueSpecification.
/// Mapeia: Req 8, Req 9, Req 12, Req 13, Req 17, PBT-04..06, ADR-0008, TASK-04.
/// </summary>
public sealed class CalculatorsAndPoliciesTests
{
    // =========================================================================
    // ForecastCalculator (PBT-04)
    // =========================================================================

    [Theory(DisplayName = "ForecastCalculator: casos exatos")]
    [InlineData(10000L, 100, 10000L)] // 100% → valor total
    [InlineData(10000L, 50, 5000L)]   // 50% → metade
    [InlineData(10000L, 0, 0L)]       // 0% → zero
    [InlineData(10001L, 50, 5000L)]   // 10001×50/100 = 5000.5 → 5000 (ToEven: par mais próximo)
    [InlineData(10003L, 50, 5002L)]   // 10003×50/100 = 5001.5 → 5002 (ToEven: par)
    public void ForecastCalculator_ExactCases(long totalCents, int prob, long expected)
    {
        var cv = new ContractValue(new Money(totalCents, "BRL"), Money.Zero("BRL"), 0);
        var probability = new Probability(prob);
        var result = ForecastCalculator.Calculate(cv, probability);
        result.AmountInCents.Should().Be(expected);
    }

    [Fact(DisplayName = "ADR-0008: ForecastCalculator preserva currency do ContractValue")]
    public void ForecastCalculator_PreservesCurrency()
    {
        var cv = new ContractValue(new Money(10000L, "USD"), Money.Zero("USD"), 0);
        var result = ForecastCalculator.Calculate(cv, new Probability(50));
        result.Currency.Should().Be("USD");
    }

    [Property(MaxTest = 200, DisplayName = "PBT-04: ForecastCalculator — resultado em [0, valor_total]")]
    public Property PBT04_ForecastCalculator_WithinBounds()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, 100_000_000).Select(x => (long)x)),
            Arb.From(Gen.Choose(0, 100)),
            (totalCents, prob) =>
            {
                var cv = new ContractValue(new Money(totalCents, "BRL"), Money.Zero("BRL"), 0);
                var probability = new Probability(prob);
                var forecast = ForecastCalculator.Calculate(cv, probability);
                return forecast.AmountInCents >= 0 && forecast.AmountInCents <= totalCents;
            });
    }

    // =========================================================================
    // CommissionCalculator (PBT-05)
    // =========================================================================

    [Fact(DisplayName = "CommissionCalculator: percentuais — calcula setup, recorrente e total")]
    public void CommissionCalculator_WithPercentages()
    {
        var cv = new ContractValue(new Money(10000L, "BRL"), new Money(5000L, "BRL"), 12);
        var terms = new CommissionTerms(CommissionRole.Revendedor, 10m, 5m, null, 12);
        var calc = CommissionCalculator.Calculate(cv, terms);

        // comissao_setup = round(10000 × 10 / 100) = 1000
        calc.ComissaoSetup.AmountInCents.Should().Be(1000L);
        // comissao_recorrente = round(5000 × 12 × 5 / 100) = 3000
        calc.ComissaoRecorrente.AmountInCents.Should().Be(3000L);
        // comissao_total = 1000 + 3000 = 4000
        calc.ComissaoTotal.AmountInCents.Should().Be(4000L);
    }

    [Fact(DisplayName = "CommissionCalculator: valor_fixo — total = valor_fixo")]
    public void CommissionCalculator_WithValorFixo()
    {
        var cv = new ContractValue(new Money(10000L, "BRL"), new Money(5000L, "BRL"), 12);
        var terms = new CommissionTerms(CommissionRole.Indicador, 0m, 0m, new Money(99900L, "BRL"), 0);
        var calc = CommissionCalculator.Calculate(cv, terms);

        calc.ComissaoTotal.AmountInCents.Should().Be(99900L);
    }

    [Fact(DisplayName = "CommissionCalculator: sem comissão retorna zeros")]
    public void CommissionCalculator_NoCommission_ReturnsZeros()
    {
        var cv = new ContractValue(new Money(10000L, "BRL"), Money.Zero("BRL"), 0);
        var terms = new CommissionTerms(CommissionRole.Revendedor, 0m, 0m, null, 0);
        var calc = CommissionCalculator.Calculate(cv, terms);

        calc.ComissaoSetup.AmountInCents.Should().Be(0L);
        calc.ComissaoRecorrente.AmountInCents.Should().Be(0L);
        calc.ComissaoTotal.AmountInCents.Should().Be(0L);
    }

    [Fact(DisplayName = "ADR-0008: CommissionCalculator preserva currency do ContractValue")]
    public void CommissionCalculator_PreservesCurrency()
    {
        var cv = new ContractValue(new Money(10000L, "USD"), new Money(5000L, "USD"), 12);
        var terms = new CommissionTerms(CommissionRole.Revendedor, 10m, 5m, null, 12);
        var calc = CommissionCalculator.Calculate(cv, terms);

        calc.ComissaoSetup.Currency.Should().Be("USD");
        calc.ComissaoRecorrente.Currency.Should().Be("USD");
        calc.ComissaoTotal.Currency.Should().Be("USD");
    }

    [Property(MaxTest = 200, DisplayName = "PBT-05: CommissionCalculator — componentes não negativos e soma correta")]
    public Property PBT05_CommissionCalculator_ComponentsNonNegativeAndSumCorrect()
    {
        // Combina setup+mensal+meses+pctSetup em tuple (Gen.Zip) para caber no limite de 4 arbitraries
        var contractAndPctsGen = Gen.Zip(
            Gen.Zip(
                Gen.Choose(0, 1_000_000).Select(x => (long)x),
                Gen.Choose(0, 1_000_000).Select(x => (long)x)),
            Gen.Zip(
                Gen.Choose(1, 60),
                Gen.Choose(0, 100).Select(x => (decimal)x)));

        return Prop.ForAll(
            Arb.From(contractAndPctsGen),
            Arb.From(Gen.Choose(0, 100).Select(x => (decimal)x)),
            (contractAndPcts, pctRecorrente) =>
            {
                var ((setupCents, mensalCents), (meses, pctSetup)) = contractAndPcts;
                var cv = new ContractValue(new Money(setupCents, "BRL"), new Money(mensalCents, "BRL"), meses);
                var terms = new CommissionTerms(CommissionRole.Revendedor, pctSetup, pctRecorrente, null, meses);
                var calc = CommissionCalculator.Calculate(cv, terms);

                var setupNonNeg = calc.ComissaoSetup.AmountInCents >= 0;
                var recorrenteNonNeg = calc.ComissaoRecorrente.AmountInCents >= 0;
                var totalCorrect = calc.ComissaoTotal.AmountInCents ==
                    calc.ComissaoSetup.AmountInCents + calc.ComissaoRecorrente.AmountInCents;

                return setupNonNeg && recorrenteNonNeg && totalCorrect;
            });
    }

    [Property(MaxTest = 200, DisplayName = "PBT-05b: CommissionCalculator valor_fixo — total = valor_fixo")]
    public Property PBT05b_CommissionCalculator_ValorFixo_TotalEqualsFixed()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, 10_000_000).Select(x => (long)x)),
            Arb.From(Gen.Choose(0, 10_000_000).Select(x => (long)x)),
            Arb.From(Gen.Choose(0, 10_000_000).Select(x => (long)x)),
            (setupCents, mensalCents, fixedCents) =>
            {
                var cv = new ContractValue(new Money(setupCents, "BRL"), new Money(mensalCents > 0 ? mensalCents : 0, "BRL"), mensalCents > 0 ? 1 : 0);
                var terms = new CommissionTerms(CommissionRole.Indicador, 0m, 0m, new Money(fixedCents, "BRL"), 0);
                var calc = CommissionCalculator.Calculate(cv, terms);
                return calc.ComissaoTotal.AmountInCents == fixedCents;
            });
    }

    // =========================================================================
    // NetForecastCalculator (PBT-06)
    // =========================================================================

    [Fact(DisplayName = "NetForecastCalculator: forecast_liquido = forecast - comissao_ponderada")]
    public void NetForecastCalculator_CorrectCalculation()
    {
        // forecast = round(10000 × 50 / 100) = 5000
        // comissao_ponderada = round(1000 × 50 / 100) = 500
        // forecast_liquido = 5000 - 500 = 4500
        var cv = new ContractValue(new Money(10000L, "BRL"), Money.Zero("BRL"), 0);
        var probability = new Probability(50);
        var commission = new CommissionCalculation(new Money(0L, "BRL"), new Money(0L, "BRL"), new Money(1000L, "BRL"));
        var result = NetForecastCalculator.Calculate(cv, probability, commission);

        result.ForecastPonderado.AmountInCents.Should().Be(5000L);
        result.ComissaoPonderada.AmountInCents.Should().Be(500L);
        result.ForecastLiquido.AmountInCents.Should().Be(4500L);
    }

    [Fact(DisplayName = "NetForecastCalculator: sem comissão, forecast_liquido = forecast_ponderado")]
    public void NetForecastCalculator_NoCommission_LiquidoEqualsForcast()
    {
        var cv = new ContractValue(new Money(10000L, "BRL"), Money.Zero("BRL"), 0);
        var probability = new Probability(50);
        var commission = new CommissionCalculation(Money.Zero("BRL"), Money.Zero("BRL"), Money.Zero("BRL"));
        var result = NetForecastCalculator.Calculate(cv, probability, commission);

        result.ForecastLiquido.AmountInCents.Should().Be(result.ForecastPonderado.AmountInCents);
    }

    [Property(MaxTest = 200, DisplayName = "PBT-06: NetForecastCalculator — forecast_liquido ≤ forecast_ponderado")]
    public Property PBT06_NetForecastCalculator_LiquidoLeForcast()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, 100_000_000).Select(x => (long)x)),
            Arb.From(Gen.Choose(0, 100)),
            Arb.From(Gen.Choose(0, 1_000_000).Select(x => (long)x)),
            (totalCents, prob, comissaoTotalCents) =>
            {
                var cv = new ContractValue(new Money(totalCents, "BRL"), Money.Zero("BRL"), 0);
                var probability = new Probability(prob);
                var commission = new CommissionCalculation(Money.Zero("BRL"), Money.Zero("BRL"), new Money(comissaoTotalCents, "BRL"));
                var result = NetForecastCalculator.Calculate(cv, probability, commission);

                return result.ForecastLiquido.AmountInCents <= result.ForecastPonderado.AmountInCents
                    && result.ForecastLiquido.AmountInCents >= 0;
            });
    }

    // =========================================================================
    // ExpectedCloseDatePolicy
    // =========================================================================

    [Theory(DisplayName = "ExpectedCloseDatePolicy: obrigatória para estágios ≥ Proposta Enviada (order 3)")]
    [InlineData(3, true)]  // Proposta Enviada
    [InlineData(4, true)]  // posterior
    [InlineData(10, true)] // final
    [InlineData(2, false)] // anterior
    [InlineData(1, false)] // inicial
    [InlineData(0, false)] // stage 0
    public void ExpectedCloseDatePolicy_IsRequired(int stageOrder, bool expected)
    {
        var propostaEnviadaOrder = 3;
        var result = ExpectedCloseDatePolicy.IsRequired(stageOrder, propostaEnviadaOrder);
        result.Should().Be(expected);
    }

    // =========================================================================
    // StagnationSpecification
    // =========================================================================

    [Fact(DisplayName = "StagnationSpecification: open com last_activity há ≥ 14 dias é stale")]
    public void StagnationSpecification_OpenWithOldActivity_IsStale()
    {
        var now = DateTimeOffset.UtcNow;
        var lastActivity = now.AddDays(-15);
        var result = StagnationSpecification.IsSatisfiedBy(
            stageCategory: StageCategory.Open,
            lastActivityAt: lastActivity,
            now: now);
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "StagnationSpecification: open com last_activity exatamente há 14 dias é stale")]
    public void StagnationSpecification_OpenWithActivity14DaysAgo_IsStale()
    {
        var now = DateTimeOffset.UtcNow;
        var lastActivity = now.AddDays(-14);
        var result = StagnationSpecification.IsSatisfiedBy(
            stageCategory: StageCategory.Open,
            lastActivityAt: lastActivity,
            now: now);
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "StagnationSpecification: open com last_activity há 13 dias não é stale")]
    public void StagnationSpecification_OpenWithActivity13DaysAgo_IsNotStale()
    {
        var now = DateTimeOffset.UtcNow;
        var lastActivity = now.AddDays(-13);
        var result = StagnationSpecification.IsSatisfiedBy(
            stageCategory: StageCategory.Open,
            lastActivityAt: lastActivity,
            now: now);
        result.Should().BeFalse();
    }

    [Theory(DisplayName = "StagnationSpecification: won/lost nunca são stale")]
    [InlineData(StageCategory.Won)]
    [InlineData(StageCategory.Lost)]
    public void StagnationSpecification_ClosedOpportunity_IsNotStale(StageCategory category)
    {
        var now = DateTimeOffset.UtcNow;
        var lastActivity = now.AddDays(-30); // muito antigo
        var result = StagnationSpecification.IsSatisfiedBy(
            stageCategory: category,
            lastActivityAt: lastActivity,
            now: now);
        result.Should().BeFalse();
    }

    // =========================================================================
    // OverdueSpecification
    // =========================================================================

    [Fact(DisplayName = "OverdueSpecification: open com data no passado é vencida")]
    public void OverdueSpecification_OpenWithPastDate_IsOverdue()
    {
        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var pastDate = now.AddDays(-1);
        var result = OverdueSpecification.IsSatisfiedBy(
            stageCategory: StageCategory.Open,
            expectedCloseDate: pastDate,
            today: now);
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "OverdueSpecification: open com data futura não é vencida")]
    public void OverdueSpecification_OpenWithFutureDate_IsNotOverdue()
    {
        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureDate = now.AddDays(1);
        var result = OverdueSpecification.IsSatisfiedBy(
            stageCategory: StageCategory.Open,
            expectedCloseDate: futureDate,
            today: now);
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "OverdueSpecification: won/lost nunca são vencidas")]
    public void OverdueSpecification_ClosedOpportunity_IsNotOverdue()
    {
        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var pastDate = now.AddDays(-10);
        var resultWon = OverdueSpecification.IsSatisfiedBy(StageCategory.Won, pastDate, now);
        var resultLost = OverdueSpecification.IsSatisfiedBy(StageCategory.Lost, pastDate, now);
        resultWon.Should().BeFalse();
        resultLost.Should().BeFalse();
    }

    [Fact(DisplayName = "OverdueSpecification: sem data de fechamento não é vencida")]
    public void OverdueSpecification_NoExpectedDate_IsNotOverdue()
    {
        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = OverdueSpecification.IsSatisfiedBy(
            stageCategory: StageCategory.Open,
            expectedCloseDate: null,
            today: now);
        result.Should().BeFalse();
    }
}
