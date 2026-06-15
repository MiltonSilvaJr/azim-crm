using OpportunityPipeline.Domain.Opportunities.Exceptions;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Tests.Opportunities.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor Money.
/// Mapeia: RNF 11, DD-004, ADR-0008, TASK-02.
/// </summary>
public sealed class MoneyTests
{
    [Fact(DisplayName = "Money: valor zero é válido")]
    public void Money_Zero_IsValid()
    {
        var money = new Money(0L, "BRL");
        money.AmountInCents.Should().Be(0L);
    }

    [Fact(DisplayName = "Money: valor positivo é válido")]
    public void Money_Positive_IsValid()
    {
        var money = new Money(1590L, "BRL");
        money.AmountInCents.Should().Be(1590L);
    }

    [Fact(DisplayName = "Money: valor negativo lança DomainException")]
    public void Money_Negative_ThrowsDomainException()
    {
        var act = () => new Money(-1L, "BRL");
        act.Should().Throw<DomainException>()
            .WithMessage("*negativo*");
    }

    [Fact(DisplayName = "Money: igualdade por valor")]
    public void Money_EqualityByValue()
    {
        var a = new Money(100L, "BRL");
        var b = new Money(100L, "BRL");
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact(DisplayName = "Money: desigualdade por valor")]
    public void Money_InequalityByValue()
    {
        var a = new Money(100L, "BRL");
        var b = new Money(200L, "BRL");
        (a != b).Should().BeTrue();
    }

    [Fact(DisplayName = "Money: soma de dois valores na mesma moeda")]
    public void Money_Add_ReturnsSum()
    {
        var a = new Money(100L, "BRL");
        var b = new Money(200L, "BRL");
        var result = a.Add(b);
        result.AmountInCents.Should().Be(300L);
    }

    [Fact(DisplayName = "Money: subtração válida")]
    public void Money_Subtract_Valid()
    {
        var a = new Money(300L, "BRL");
        var b = new Money(100L, "BRL");
        var result = a.Subtract(b);
        result.AmountInCents.Should().Be(200L);
    }

    [Fact(DisplayName = "Money: subtração que resulta em negativo lança DomainException")]
    public void Money_Subtract_ResultNegative_ThrowsDomainException()
    {
        var a = new Money(100L, "BRL");
        var b = new Money(200L, "BRL");
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

    [Fact(DisplayName = "Money: Zero(currency) retorna instância com valor 0")]
    public void Money_ZeroMethod_ReturnsZeroAmount()
    {
        var zero = Money.Zero("BRL");
        zero.AmountInCents.Should().Be(0L);
        zero.Currency.Should().Be("BRL");
    }

    // =========================================================================
    // ADR-0008 — suporte multimoeda BRL/USD/EUR
    // =========================================================================

    [Theory(DisplayName = "ADR-0008: moedas suportadas BRL/USD/EUR são aceitas")]
    [InlineData("BRL")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void Money_SupportedCurrencies_AreAccepted(string currency)
    {
        var act = () => new Money(100L, currency);
        act.Should().NotThrow();
    }

    [Theory(DisplayName = "ADR-0008: moeda inválida lança DomainException")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    [InlineData("")]
    [InlineData("brl")]
    [InlineData("REAL")]
    public void Money_InvalidCurrency_ThrowsDomainException(string currency)
    {
        var act = () => new Money(100L, currency);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "ADR-0008: Add com moedas diferentes lança DomainException")]
    public void Money_Add_DifferentCurrencies_ThrowsDomainException()
    {
        var brl = new Money(100L, "BRL");
        var usd = new Money(100L, "USD");
        var act = () => brl.Add(usd);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "ADR-0008: Subtract com moedas diferentes lança DomainException")]
    public void Money_Subtract_DifferentCurrencies_ThrowsDomainException()
    {
        var usd = new Money(300L, "USD");
        var eur = new Money(100L, "EUR");
        var act = () => usd.Subtract(eur);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "ADR-0008: Currency preservada após operações")]
    public void Money_Currency_PreservedAfterOperations()
    {
        var a = new Money(100L, "USD");
        var b = new Money(50L, "USD");
        a.Add(b).Currency.Should().Be("USD");
        a.Subtract(b).Currency.Should().Be("USD");
    }

    [Fact(DisplayName = "ADR-0008: SupportedCurrencies contém exatamente BRL, USD, EUR")]
    public void Money_SupportedCurrencies_ContainsExactlyThree()
    {
        Money.SupportedCurrencies.Should().BeEquivalentTo(new[] { "BRL", "USD", "EUR" });
    }

    [Fact(DisplayName = "ADR-0008: Zero(USD) retorna Money USD com 0 centavos")]
    public void Money_Zero_USD_ReturnsZeroUSD()
    {
        var zero = Money.Zero("USD");
        zero.AmountInCents.Should().Be(0L);
        zero.Currency.Should().Be("USD");
    }

    [Property(MaxTest = 200, DisplayName = "Money: PBT — valores não negativos sempre válidos em BRL")]
    public Property PBT_Money_NonNegativeAlwaysValid()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, int.MaxValue).Select(x => (long)x)),
            amountInCents =>
            {
                var money = new Money(amountInCents, "BRL");
                return money.AmountInCents == amountInCents;
            });
    }

    [Property(MaxTest = 200, DisplayName = "Money: PBT — soma é comutativa (BRL)")]
    public Property PBT_Money_AddIsCommutative()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, 500_000).Select(x => (long)x)),
            Arb.From(Gen.Choose(0, 500_000).Select(x => (long)x)),
            (a, b) =>
            {
                var ma = new Money(a, "BRL");
                var mb = new Money(b, "BRL");
                return ma.Add(mb) == mb.Add(ma);
            });
    }

    [Property(MaxTest = 100, DisplayName = "ADR-0008: PBT — Add com moedas diferentes sempre lança DomainException")]
    public Property PBT_Money_Add_CrossCurrency_AlwaysThrows()
    {
        var currencies = new[] { "BRL", "USD", "EUR" };
        var pairsGen = Gen.Choose(0, 2).Select(i => currencies[i]);

        return Prop.ForAll(
            Arb.From(Gen.Choose(0, 100_000).Select(x => (long)x)),
            Arb.From(Gen.Choose(0, 100_000).Select(x => (long)x)),
            (a, b) =>
            {
                var ma = new Money(a, "BRL");
                var mb = new Money(b, "USD");
                try
                {
                    ma.Add(mb);
                    return false; // não deveria chegar aqui
                }
                catch (DomainException)
                {
                    return true;
                }
            });
    }
}
