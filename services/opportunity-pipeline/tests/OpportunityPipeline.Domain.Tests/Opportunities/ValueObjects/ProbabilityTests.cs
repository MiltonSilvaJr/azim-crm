using OpportunityPipeline.Domain.Opportunities.Exceptions;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Tests.Opportunities.ValueObjects;

/// <summary>
/// Testes para Probability — intervalo [0, 100].
/// Mapeia: Req 7, INV-9, TASK-02.
/// </summary>
public sealed class ProbabilityTests
{
    [Theory(DisplayName = "Probability: valores válidos no intervalo [0, 100]")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    public void Probability_Valid(int value)
    {
        var p = new Probability(value);
        p.Value.Should().Be(value);
    }

    [Fact(DisplayName = "Probability(-1) lança DomainException — abaixo do limite")]
    public void Probability_NegativeOne_ThrowsDomainException()
    {
        var act = () => new Probability(-1);
        act.Should().Throw<DomainException>()
            .WithMessage("*[0, 100]*");
    }

    [Fact(DisplayName = "Probability(101) lança DomainException — acima do limite")]
    public void Probability_OneHundredOne_ThrowsDomainException()
    {
        var act = () => new Probability(101);
        act.Should().Throw<DomainException>()
            .WithMessage("*[0, 100]*");
    }

    [Theory(DisplayName = "Probability: valores inválidos lançam DomainException")]
    [InlineData(-100)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(200)]
    public void Probability_OutOfRange_ThrowsDomainException(int value)
    {
        var act = () => new Probability(value);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "Probability: igualdade por valor")]
    public void Probability_EqualityByValue()
    {
        var a = new Probability(75);
        var b = new Probability(75);
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Property(MaxTest = 200, DisplayName = "Probability: PBT — valores em [0,100] sempre válidos")]
    public Property PBT_Probability_ValidRange()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, 100)),
            value =>
            {
                var p = new Probability(value);
                return p.Value == value;
            });
    }

    [Property(MaxTest = 200, DisplayName = "Probability: PBT — valores fora de [0,100] sempre lançam exceção")]
    public Property PBT_Probability_InvalidRange()
    {
        var outsideRange = Gen.OneOf(
            Gen.Choose(-10000, -1),
            Gen.Choose(101, 10000));

        return Prop.ForAll(
            Arb.From(outsideRange),
            value =>
            {
                try
                {
                    _ = new Probability(value);
                    return false;
                }
                catch (DomainException)
                {
                    return true;
                }
            });
    }
}
