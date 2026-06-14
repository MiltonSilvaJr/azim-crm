using DataMigration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace DataMigration.Domain.Tests.ValueObjects;

/// <summary>
/// Testes dos objetos de valor auxiliares: <see cref="NormalizedName"/>,
/// <see cref="Money"/>, <see cref="TriageFlag"/>, <see cref="SourceRow"/>.
///
/// Cobre: TASK-07 (ST-01), design §4.3, Req 3, 5, 7.
/// </summary>
public sealed class AuxiliaryValueObjectTests
{
    // =========================================================================
    // NormalizedName
    // =========================================================================

    [Theory(DisplayName = "NormalizedName deve aplicar trim e comparação insensível a case")]
    [InlineData("Pag.ai",  "pag.ai",   true)]
    [InlineData("  Azim ", "azim",      true)]
    [InlineData("Azim",    "Azim",      true)]
    [InlineData("Azim",    "azimx",     false)]
    public void NormalizedName_ShouldNormalize_AndCompare_CaseInsensitive(
        string a, string b, bool shouldBeEqual)
    {
        var na = NormalizedName.From(a);
        var nb = NormalizedName.From(b);
        if (shouldBeEqual)
        {
            na.Should().Be(nb);
        }
        else
        {
            na.Should().NotBe(nb);
        }
    }

    [Fact(DisplayName = "NormalizedName deve preservar o valor original normalizado")]
    public void NormalizedName_ShouldStore_TrimmedLowercaseValue()
    {
        var name = NormalizedName.From("  Vellus Assessoria  ");
        name.Value.Should().Be("vellus assessoria");
    }

    [Fact(DisplayName = "NormalizedName deve rejeitar string vazia ou nula")]
    public void NormalizedName_ShouldReject_EmptyOrNull()
    {
        var act1 = () => NormalizedName.From("");
        var act2 = () => NormalizedName.From("   ");
        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
    }

    // =========================================================================
    // Money
    // =========================================================================

    [Fact(DisplayName = "Money deve armazenar valor em centavos como long")]
    public void Money_ShouldStore_ValueInCents()
    {
        var money = Money.OfCents(1590L);
        money.AmountInCents.Should().Be(1590L);
        money.Currency.Should().Be("BRL");
    }

    [Fact(DisplayName = "Money.Zero deve ter amountInCents = 0")]
    public void Money_Zero_ShouldHaveZeroCents()
    {
        Money.Zero.AmountInCents.Should().Be(0L);
    }

    [Fact(DisplayName = "Money deve rejeitar valor negativo")]
    public void Money_ShouldReject_NegativeValue()
    {
        var act = () => Money.OfCents(-1L);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Money deve ter igualdade por valor")]
    public void Money_ShouldHave_ValueEquality()
    {
        var a = Money.OfCents(1000L);
        var b = Money.OfCents(1000L);
        a.Should().Be(b);
    }

    [Fact(DisplayName = "Money com valores diferentes deve ser diferente")]
    public void Money_DifferentValues_ShouldNotBeEqual()
    {
        var a = Money.OfCents(1000L);
        var b = Money.OfCents(2000L);
        a.Should().NotBe(b);
    }

    // =========================================================================
    // TriageFlag
    // =========================================================================

    [Fact(DisplayName = "TriageFlag deve criar flag com tipo, severidade e índice de linha")]
    public void TriageFlag_ShouldCreate_WithCorrectFields()
    {
        var flag = new TriageFlag(
            flagType: TriageFlagType.OwnerMissing,
            severity: TriageSeverity.Blocking,
            sourceRowIndex: 42);

        flag.FlagType.Should().Be(TriageFlagType.OwnerMissing);
        flag.Severity.Should().Be(TriageSeverity.Blocking);
        flag.SourceRowIndex.Should().Be(42);
    }

    [Theory(DisplayName = "TriageFlag bloqueante: owner_missing; não-bloqueante: demais")]
    [InlineData(TriageFlagType.OwnerMissing, TriageSeverity.Blocking)]
    [InlineData(TriageFlagType.StageMissing, TriageSeverity.NonBlocking)]
    [InlineData(TriageFlagType.PartnerPctMissing, TriageSeverity.NonBlocking)]
    [InlineData(TriageFlagType.DedupeCandidate, TriageSeverity.NonBlocking)]
    [InlineData(TriageFlagType.BuUnknown, TriageSeverity.NonBlocking)]
    [InlineData(TriageFlagType.Typo, TriageSeverity.NonBlocking)]
    public void TriageFlag_SeverityByType_ShouldBeCorrect(
        TriageFlagType flagType, TriageSeverity expectedSeverity)
    {
        var flag = TriageFlag.ForRow(flagType, sourceRowIndex: 1);
        flag.Severity.Should().Be(expectedSeverity);
    }

    [Fact(DisplayName = "TriageFlag deve ter igualdade por valor")]
    public void TriageFlag_ShouldHave_ValueEquality()
    {
        var a = new TriageFlag(TriageFlagType.OwnerMissing, TriageSeverity.Blocking, 5);
        var b = new TriageFlag(TriageFlagType.OwnerMissing, TriageSeverity.Blocking, 5);
        a.Should().Be(b);
    }

    // =========================================================================
    // SourceRow
    // =========================================================================

    [Fact(DisplayName = "SourceRow deve ser imutável com igualdade por valor")]
    public void SourceRow_ShouldBeImmutable_WithValueEquality()
    {
        var cells = new Dictionary<string, string?> { ["col1"] = "val1" };
        var a = new SourceRow(sheetName: "pipeline", rowIndex: 5, cells: cells);
        var b = new SourceRow(sheetName: "pipeline", rowIndex: 5, cells: cells);

        a.Should().Be(b);
        a.SheetName.Should().Be("pipeline");
        a.RowIndex.Should().Be(5);
    }

    [Fact(DisplayName = "SourceRow com índice diferente deve ser diferente")]
    public void SourceRow_DifferentIndex_ShouldNotBeEqual()
    {
        var cells = new Dictionary<string, string?>();
        var a = new SourceRow("pipeline", 1, cells);
        var b = new SourceRow("pipeline", 2, cells);
        a.Should().NotBe(b);
    }
}
