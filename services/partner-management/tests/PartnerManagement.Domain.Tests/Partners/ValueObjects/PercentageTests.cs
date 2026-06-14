using FluentAssertions;
using PartnerManagement.Domain.Partners.Exceptions;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Domain.Tests.Partners.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="Percentage"/>.
/// Mapeia: Req 6.3, RNF 6, design §4.3, DD-004, TASK-03.
/// Percentuais são decimal (NUMERIC 5,2) — proibido float/double (RNF 6.3).
/// </summary>
public sealed class PercentageTests
{
    [Theory(DisplayName = "Percentage — valor abaixo de zero lança PercentageOutOfRangeException")]
    [InlineData(-0.01)]
    [InlineData(-1.00)]
    [InlineData(-100.00)]
    public void Create_BelowZero_ThrowsPercentageOutOfRangeException(double raw)
    {
        decimal value = (decimal)raw;
        Action act = () => Percentage.Create(value);
        act.Should().Throw<PercentageOutOfRangeException>();
    }

    [Theory(DisplayName = "Percentage — valor acima de 100 lança PercentageOutOfRangeException")]
    [InlineData(100.01)]
    [InlineData(101.00)]
    [InlineData(200.00)]
    public void Create_AboveOneHundred_ThrowsPercentageOutOfRangeException(double raw)
    {
        decimal value = (decimal)raw;
        Action act = () => Percentage.Create(value);
        act.Should().Throw<PercentageOutOfRangeException>();
    }

    [Theory(DisplayName = "Percentage — valores válidos no intervalo [0,00; 100,00] são aceitos")]
    [InlineData("0.00")]
    [InlineData("0.01")]
    [InlineData("50.00")]
    [InlineData("99.99")]
    [InlineData("100.00")]
    public void Create_ValidRange_Succeeds(string raw)
    {
        decimal value = decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        Action act = () => Percentage.Create(value);
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "Percentage — valor preserva exatamente 2 casas decimais (round-trip)")]
    public void Create_ValidValue_PreservesExactly2DecimalPlaces()
    {
        Percentage pct = Percentage.Create(33.33m);
        pct.Value.Should().Be(33.33m);
    }

    [Fact(DisplayName = "Percentage — zero é valor válido (default CommissionDefaults)")]
    public void Create_Zero_IsValid()
    {
        Percentage pct = Percentage.Create(0.00m);
        pct.Value.Should().Be(0.00m);
    }

    [Fact(DisplayName = "Percentage — igualdade por valor: mesmos percentuais são iguais")]
    public void EqualityByValue_SameValues_AreEqual()
    {
        Percentage a = Percentage.Create(15.50m);
        Percentage b = Percentage.Create(15.50m);
        a.Should().Be(b);
    }

    [Fact(DisplayName = "Percentage — igualdade por valor: percentuais diferentes não são iguais")]
    public void EqualityByValue_DifferentValues_AreNotEqual()
    {
        Percentage a = Percentage.Create(10.00m);
        Percentage b = Percentage.Create(20.00m);
        a.Should().NotBe(b);
    }

    [Fact(DisplayName = "Percentage — Value é do tipo decimal, não float nem double (RNF 6.3, DD-004)")]
    public void Value_IsDecimalType()
    {
        Percentage pct = Percentage.Create(25.00m);
        // Verificação estrutural: decimal.MaxValue atribuível a Value confirma o tipo
        decimal check = pct.Value;
        check.Should().Be(25.00m);
    }
}
