using OpportunityPipeline.Domain.Opportunities.Exceptions;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Tests.Opportunities.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor Money.
/// Mapeia: RNF 11, DD-004, TASK-02.
/// </summary>
public sealed class MoneyTests
{
    [Fact(DisplayName = "Money: valor zero é válido")]
    public void Money_Zero_IsValid()
    {
        var money = new Money(0L);
        money.AmountInCents.Should().Be(0L);
    }

    [Fact(DisplayName = "Money: valor positivo é válido")]
    public void Money_Positive_IsValid()
    {
        var money = new Money(1590L);
        money.AmountInCents.Should().Be(1590L);
    }

    [Fact(DisplayName = "Money: valor negativo lança DomainException")]
    public void Money_Negative_ThrowsDomainException()
    {
        var act = () => new Money(-1L);
        act.Should().Throw<DomainException>()
            .WithMessage("*negativo*");
    }

    [Fact(DisplayName = "Money: igualdade por valor")]
    public void Money_EqualityByValue()
    {
        var a = new Money(100L);
        var b = new Money(100L);
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact(DisplayName = "Money: desigualdade por valor")]
    public void Money_InequalityByValue()
    {
        var a = new Money(100L);
        var b = new Money(200L);
        (a != b).Should().BeTrue();
    }

    [Fact(DisplayName = "Money: soma de dois valores")]
    public void Money_Add_ReturnsSum()
    {
        var a = new Money(100L);
        var b = new Money(200L);
        var result = a.Add(b);
        result.AmountInCents.Should().Be(300L);
    }

    [Fact(DisplayName = "Money: subtração válida")]
    public void Money_Subtract_Valid()
    {
        var a = new Money(300L);
        var b = new Money(100L);
        var result = a.Subtract(b);
        result.AmountInCents.Should().Be(200L);
    }

    [Fact(DisplayName = "Money: subtração que resulta em negativo lança DomainException")]
    public void Money_Subtract_ResultNegative_ThrowsDomainException()
    {
        var a = new Money(100L);
        var b = new Money(200L);
        var act = () => a.Subtract(b);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "Money: sem construtores com float ou double")]
    public void Money_NoFloatOrDoubleConstructor()
    {
        var ctors = typeof(Money).GetConstructors();
        foreach (var ctor in ctors)
        {
            var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToList();
            paramTypes.Should().NotContain(typeof(float), because: "Money não deve aceitar float");
            paramTypes.Should().NotContain(typeof(double), because: "Money não deve aceitar double");
        }
    }

    [Fact(DisplayName = "Money: Zero é constante utilitária com valor 0")]
    public void Money_Zero_Constant()
    {
        Money.Zero.AmountInCents.Should().Be(0L);
    }

    [Property(MaxTest = 200, DisplayName = "Money: PBT — valores não negativos sempre válidos")]
    public Property PBT_Money_NonNegativeAlwaysValid()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, int.MaxValue).Select(x => (long)x)),
            amountInCents =>
            {
                var money = new Money(amountInCents);
                return money.AmountInCents == amountInCents;
            });
    }

    [Property(MaxTest = 200, DisplayName = "Money: PBT — soma é comutativa")]
    public Property PBT_Money_AddIsCommutative()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, 500_000).Select(x => (long)x)),
            Arb.From(Gen.Choose(0, 500_000).Select(x => (long)x)),
            (a, b) =>
            {
                var ma = new Money(a);
                var mb = new Money(b);
                return ma.Add(mb) == mb.Add(ma);
            });
    }
}
