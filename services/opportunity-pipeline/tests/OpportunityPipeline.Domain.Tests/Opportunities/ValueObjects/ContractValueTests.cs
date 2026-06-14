using OpportunityPipeline.Domain.Opportunities.Exceptions;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Tests.Opportunities.ValueObjects;

/// <summary>
/// Testes para ContractValue — PBT-03 (invariante TCV).
/// Mapeia: Req 8, RNF 11, TASK-02.
/// </summary>
public sealed class ContractValueTests
{
    [Fact(DisplayName = "ContractValue: valor_total = setup + mensal × meses")]
    public void ContractValue_TotalIsCorrect()
    {
        var cv = new ContractValue(new Money(1000L), new Money(500L), 12);
        cv.TotalInCents.Should().Be(1000L + 500L * 12); // 7000
    }

    [Fact(DisplayName = "ContractValue: só setup, sem mensal")]
    public void ContractValue_SetupOnly()
    {
        var cv = new ContractValue(new Money(5000L), Money.Zero, 0);
        cv.TotalInCents.Should().Be(5000L);
    }

    [Fact(DisplayName = "ContractValue: zero setup e mensal")]
    public void ContractValue_AllZero()
    {
        var cv = new ContractValue(Money.Zero, Money.Zero, 0);
        cv.TotalInCents.Should().Be(0L);
    }

    [Fact(DisplayName = "ContractValue: valor_mensal > 0 com duracao_meses = 0 lança DomainException")]
    public void ContractValue_MensalWithoutMeses_ThrowsDomainException()
    {
        var act = () => new ContractValue(Money.Zero, new Money(100L), 0);
        act.Should().Throw<DomainException>()
            .WithMessage("*duracao_meses*");
    }

    [Fact(DisplayName = "ContractValue: duracao_meses negativo lança DomainException")]
    public void ContractValue_NegativeMeses_ThrowsDomainException()
    {
        var act = () => new ContractValue(Money.Zero, Money.Zero, -1);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "ContractValue: setup negativo não é permitido (via Money)")]
    public void ContractValue_NegativeSetup_ThrowsDomainException()
    {
        var act = () => new ContractValue(new Money(-1L), Money.Zero, 0);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "ContractValue: igualdade por valor")]
    public void ContractValue_EqualityByValue()
    {
        var a = new ContractValue(new Money(1000L), new Money(500L), 12);
        var b = new ContractValue(new Money(1000L), new Money(500L), 12);
        a.Should().Be(b);
    }

    /// <summary>
    /// PBT-03: para quaisquer setup ≥ 0, mensal ≥ 0, meses ≥ 1 (quando mensal > 0),
    /// valor_total = setup + mensal × meses, sem perda de precisão.
    /// Mínimo 100 amostras FsCheck (tasks.md §1.2).
    /// </summary>
    [Property(MaxTest = 200, DisplayName = "PBT-03: ContractValue — valor_total = setup + mensal × meses (sem perda de precisão)")]
    public Property PBT03_ContractValue_TotalInvariant()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, 1_000_000).Select(x => (long)x)),
            Arb.From(Gen.Choose(0, 1_000_000).Select(x => (long)x)),
            Arb.From(Gen.Choose(1, 120).Select(x => x)),
            (setupCents, mensalCents, meses) =>
            {
                var cv = new ContractValue(new Money(setupCents), new Money(mensalCents), meses);
                var expected = setupCents + mensalCents * meses;
                return cv.TotalInCents == expected;
            });
    }

    /// <summary>
    /// PBT-03b: setup apenas (sem mensal) — total = setup.
    /// </summary>
    [Property(MaxTest = 200, DisplayName = "PBT-03b: ContractValue — sem mensal, total = setup")]
    public Property PBT03b_ContractValue_SetupOnly_Total()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, 10_000_000).Select(x => (long)x)),
            setupCents =>
            {
                var cv = new ContractValue(new Money(setupCents), Money.Zero, 0);
                return cv.TotalInCents == setupCents;
            });
    }
}
