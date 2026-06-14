using FluentAssertions;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Domain.Tests.Partners.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="CommissionDefaults"/>.
/// Mapeia: Req 6, PBT-03, design §4.3, DD-004, TASK-03.
/// </summary>
public sealed class CommissionDefaultsTests
{
    [Fact(DisplayName = "CommissionDefaults — Default retorna 0,00 / 0,00")]
    public void Default_ReturnsBothPercentagesAsZero()
    {
        CommissionDefaults defaults = CommissionDefaults.Default;
        defaults.PctSetup.Value.Should().Be(0.00m);
        defaults.PctRecorrente.Value.Should().Be(0.00m);
    }

    [Fact(DisplayName = "CommissionDefaults — Create com valores válidos preserva ambos os percentuais")]
    public void Create_ValidPercentages_PreservesBothValues()
    {
        Percentage setup = Percentage.Create(10.00m);
        Percentage recorrente = Percentage.Create(5.50m);
        CommissionDefaults defaults = CommissionDefaults.Create(setup, recorrente);
        defaults.PctSetup.Value.Should().Be(10.00m);
        defaults.PctRecorrente.Value.Should().Be(5.50m);
    }

    [Fact(DisplayName = "CommissionDefaults — igualdade por valor: mesmas instâncias são iguais")]
    public void EqualityByValue_SameValues_AreEqual()
    {
        CommissionDefaults a = CommissionDefaults.Create(Percentage.Create(10.00m), Percentage.Create(5.00m));
        CommissionDefaults b = CommissionDefaults.Create(Percentage.Create(10.00m), Percentage.Create(5.00m));
        a.Should().Be(b);
    }

    [Fact(DisplayName = "CommissionDefaults — igualdade por valor: valores diferentes não são iguais")]
    public void EqualityByValue_DifferentValues_AreNotEqual()
    {
        CommissionDefaults a = CommissionDefaults.Create(Percentage.Create(10.00m), Percentage.Create(5.00m));
        CommissionDefaults b = CommissionDefaults.Create(Percentage.Create(20.00m), Percentage.Create(5.00m));
        a.Should().NotBe(b);
    }

    [Fact(DisplayName = "CommissionDefaults — Default tem IsTriagePending = true (ambos em 0,00)")]
    public void Default_IsTriagePending()
    {
        CommissionDefaults defaults = CommissionDefaults.Default;
        defaults.IsTriagePending.Should().BeTrue();
    }

    [Fact(DisplayName = "CommissionDefaults — percentual não-zero faz IsTriagePending = false")]
    public void Create_NonZeroPctSetup_IsNotTriagePending()
    {
        CommissionDefaults defaults = CommissionDefaults.Create(Percentage.Create(10.00m), Percentage.Create(0.00m));
        defaults.IsTriagePending.Should().BeFalse();
    }

    [Fact(DisplayName = "CommissionDefaults — ambos não-zero também faz IsTriagePending = false")]
    public void Create_BothNonZero_IsNotTriagePending()
    {
        CommissionDefaults defaults = CommissionDefaults.Create(Percentage.Create(5.00m), Percentage.Create(3.00m));
        defaults.IsTriagePending.Should().BeFalse();
    }
}
