using FluentAssertions;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Domain.Tests.ValueObjects;

/// <summary>
/// Testes complementares para garantir cobertura ≥ 95% nos objetos de valor do Domain.
/// Cobre métodos utilitários (<c>IsWithinSloWindow</c>, <c>ToString</c>) e propriedades
/// não exercitadas nos testes focados em invariantes.
///
/// Mapeia: TASK-03, design §4.3, DD-007, DD-010.
/// </summary>
public sealed class ValueObjectsCoverageTests
{
    // -------------------------------------------------------------------------
    // Period — IsWithinSloWindow e ToString
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Period.IsWithinSloWindow deve retornar true para janela ≤ 12 meses (RNF 1)")]
    public void Period_IsWithinSloWindow_ShouldReturnTrue_ForWindowUpTo12Months()
    {
        var period = Period.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        period.IsWithinSloWindow().Should().BeTrue();
    }

    [Fact(DisplayName = "Period.IsWithinSloWindow deve retornar true para janela de 1 dia (RNF 1)")]
    public void Period_IsWithinSloWindow_ShouldReturnTrue_ForSingleDay()
    {
        var date = new DateOnly(2026, 6, 14);
        var period = Period.Create(date, date);

        period.IsWithinSloWindow().Should().BeTrue();
    }

    [Fact(DisplayName = "Period.IsWithinSloWindow deve retornar false para janela > 12 meses")]
    public void Period_IsWithinSloWindow_ShouldReturnFalse_ForWindowAbove12Months()
    {
        // 2 anos
        var period = Period.Create(new DateOnly(2024, 1, 1), new DateOnly(2025, 12, 31));

        period.IsWithinSloWindow().Should().BeFalse();
    }

    [Fact(DisplayName = "Period.ToString deve formatar datas no padrão yyyy-MM-dd")]
    public void Period_ToString_ShouldFormatDatesCorrectly()
    {
        var period = Period.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        var str = period.ToString();

        str.Should().Contain("2026-01-01").And.Contain("2026-06-30");
    }

    // -------------------------------------------------------------------------
    // Money — ToString
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Money.ToString deve incluir o valor em cents")]
    public void Money_ToString_ShouldIncludeCents()
    {
        var money = Money.FromCents(150_000L);

        var str = money.ToString();

        str.Should().Contain("150000");
    }

    [Fact(DisplayName = "Money.Zero.ToString deve indicar zero cents")]
    public void Money_Zero_ToString_ShouldIndicateZero()
    {
        var str = Money.Zero.ToString();

        str.Should().Contain("0");
    }

    // -------------------------------------------------------------------------
    // ChannelShare — propriedades não exercitadas e ToString
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "ChannelShare.ChannelId deve ser retornado corretamente")]
    public void ChannelShare_ChannelId_ShouldBeReturnedCorrectly()
    {
        var id = Guid.NewGuid();
        var share = ChannelShare.Create(id, "Canal", 5, 100_000L, 2_500);

        share.ChannelId.Should().Be(id);
    }

    [Fact(DisplayName = "ChannelShare.ChannelName deve ser retornado corretamente")]
    public void ChannelShare_ChannelName_ShouldBeReturnedCorrectly()
    {
        var share = ChannelShare.Create(Guid.NewGuid(), "Indicação Digital", 5, 100_000L, 5_000);

        share.ChannelName.Should().Be("Indicação Digital");
    }

    [Fact(DisplayName = "ChannelShare.Count deve ser retornado corretamente")]
    public void ChannelShare_Count_ShouldBeReturnedCorrectly()
    {
        var share = ChannelShare.Create(Guid.NewGuid(), "Canal", 42, 420_000L, 3_000);

        share.Count.Should().Be(42);
    }

    [Fact(DisplayName = "ChannelShare.ToString deve incluir nome e basis points")]
    public void ChannelShare_ToString_ShouldIncludeNameAndBasisPoints()
    {
        var share = ChannelShare.Create(Guid.NewGuid(), "Parceiro", 10, 100_000L, 2_500);

        var str = share.ToString();

        str.Should().Contain("Parceiro").And.Contain("2500");
    }

    // -------------------------------------------------------------------------
    // StageBucket — ToString
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "StageBucket.ToString deve incluir nome e categoria do estágio")]
    public void StageBucket_ToString_ShouldIncludeNameAndCategory()
    {
        var bucket = StageBucket.Create(Guid.NewGuid(), "Proposta Enviada", StageCategory.Open);

        var str = bucket.ToString();

        str.Should().Contain("Proposta Enviada").And.Contain("Open");
    }
}
