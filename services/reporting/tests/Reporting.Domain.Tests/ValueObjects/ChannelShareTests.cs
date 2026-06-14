using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários e PBT-04 para o objeto de valor <see cref="ChannelShare"/>.
///
/// Invariantes verificadas:
/// - <c>percentBasisPoints</c> ∈ [0, 10000].
/// - Imutabilidade e igualdade por valor.
/// - PBT-04: validação de que listas de <see cref="ChannelShare"/> com soma = 10.000
///   são construíveis com todos os valores dentro da invariante.
///
/// Mapeia: TASK-03, design §4.3, DD-010, PBT-04.
/// </summary>
public sealed class ChannelShareTests
{
    // -------------------------------------------------------------------------
    // Construção válida
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "ChannelShare com percentBasisPoints = 0 deve ser válido (DD-010)")]
    public void ChannelShare_WithZeroPercent_ShouldBeValid()
    {
        var act = () => ChannelShare.Create(
            channelId: Guid.NewGuid(),
            channelName: "Canal Inativo",
            count: 0,
            totalCents: 0L,
            percentBasisPoints: 0);

        act.Should().NotThrow();
    }

    [Fact(DisplayName = "ChannelShare com percentBasisPoints = 10000 deve ser válido (DD-010)")]
    public void ChannelShare_WithFullPercent_ShouldBeValid()
    {
        var act = () => ChannelShare.Create(
            channelId: Guid.NewGuid(),
            channelName: "Canal Único",
            count: 100,
            totalCents: 500_000L,
            percentBasisPoints: 10_000);

        act.Should().NotThrow();
    }

    [Fact(DisplayName = "ChannelShare com percentBasisPoints intermediário deve ser válido (DD-010)")]
    public void ChannelShare_WithIntermediatePercent_ShouldBeValid()
    {
        var share = ChannelShare.Create(
            channelId: Guid.NewGuid(),
            channelName: "Indicação",
            count: 10,
            totalCents: 1_000_000L,
            percentBasisPoints: 3_333);

        share.PercentBasisPoints.Should().Be(3_333);
    }

    // -------------------------------------------------------------------------
    // Rejeição de invariantes
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "ChannelShare com percentBasisPoints < 0 deve lançar (DD-010)")]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void ChannelShare_WithNegativePercent_ShouldThrow(int basisPoints)
    {
        var act = () => ChannelShare.Create(
            channelId: Guid.NewGuid(),
            channelName: "Canal",
            count: 0,
            totalCents: 0L,
            percentBasisPoints: basisPoints);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*percentBasisPoints*");
    }

    [Theory(DisplayName = "ChannelShare com percentBasisPoints > 10000 deve lançar (DD-010)")]
    [InlineData(10_001)]
    [InlineData(20_000)]
    [InlineData(int.MaxValue)]
    public void ChannelShare_WithPercentAbove100_ShouldThrow(int basisPoints)
    {
        var act = () => ChannelShare.Create(
            channelId: Guid.NewGuid(),
            channelName: "Canal",
            count: 10,
            totalCents: 100_000L,
            percentBasisPoints: basisPoints);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*percentBasisPoints*");
    }

    // -------------------------------------------------------------------------
    // Imutabilidade e igualdade por valor
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "ChannelShare com mesmos campos deve ser igual por valor (DD-010)")]
    public void ChannelShare_WithSameFields_ShouldBeEqualByValue()
    {
        var id = Guid.NewGuid();
        var a = ChannelShare.Create(id, "Canal A", 5, 100_000L, 2_500);
        var b = ChannelShare.Create(id, "Canal A", 5, 100_000L, 2_500);

        a.Should().Be(b);
    }

    [Fact(DisplayName = "ChannelShare deve ser imutável (DD-010)")]
    public void ChannelShare_ShouldBeImmutable()
    {
        var share = ChannelShare.Create(Guid.NewGuid(), "Canal", 10, 500_000L, 5_000);

        // Verificar campos não mudam (record imutável)
        share.TotalCents.Should().Be(500_000L);
        share.PercentBasisPoints.Should().Be(5_000);
    }

    // -------------------------------------------------------------------------
    // PBT-04: conservação da distribuição por canal (soma = 10.000 = 100%)
    // -------------------------------------------------------------------------

    /// <summary>
    /// PBT-04: para qualquer número de canais N (1..10), ao dividir 10.000 em N partes
    /// inteiras que somam 10.000, todos os <see cref="ChannelShare"/> são construíveis
    /// e a soma dos <c>percentBasisPoints</c> é exatamente 10.000.
    ///
    /// Mapeia: PBT-04, design §4.3, DD-010.
    /// </summary>
    [Property(MaxTest = 500, DisplayName = "PBT-04: lista de ChannelShare com soma = 10.000 é sempre construível e conserva soma (PBT-04, DD-010)")]
    public Property ChannelShare_Distribution_ConservesSumOf10000(PositiveInt channelCount)
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(channelCount)),
            n =>
            {
                var count = n.Get % 10 + 1; // 1 a 10 canais
                var parts = SplitIntoNParts(10_000, count);

                // Todos os basis points devem estar em [0, 10000] para que o Create não lance
                var allValid = parts.All(bp => bp >= 0 && bp <= 10_000);
                if (!allValid)
                {
                    return false;
                }

                var shares = parts.Select((bp, i) =>
                    ChannelShare.Create(Guid.NewGuid(), $"Canal {i + 1}", i * 2, (long)(i * 100_000), bp)).ToList();

                var sum = shares.Sum(s => s.PercentBasisPoints);
                return sum == 10_000;
            });
    }

    [Fact(DisplayName = "PBT-04: lista de ChannelShare com soma = 10000 deve ser válida (PBT-04)")]
    public void ChannelShare_ListSummingTo10000_ShouldAllBeValid()
    {
        // 4 canais: 40%, 30%, 20%, 10% em basis points
        var shares = new[]
        {
            ChannelShare.Create(Guid.NewGuid(), "Canal A", 40, 400_000L, 4_000),
            ChannelShare.Create(Guid.NewGuid(), "Canal B", 30, 300_000L, 3_000),
            ChannelShare.Create(Guid.NewGuid(), "Canal C", 20, 200_000L, 2_000),
            ChannelShare.Create(Guid.NewGuid(), "Canal D", 10, 100_000L, 1_000),
        };

        shares.Sum(s => s.PercentBasisPoints).Should().Be(10_000);
    }

    [Fact(DisplayName = "PBT-04: um único canal deve poder ter 100% (10000 bp) (PBT-04)")]
    public void ChannelShare_SingleChannel_CanHaveFullDistribution()
    {
        var share = ChannelShare.Create(Guid.NewGuid(), "Canal Único", 100, 1_000_000L, 10_000);

        share.PercentBasisPoints.Should().Be(10_000);
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Divide <paramref name="total"/> em <paramref name="n"/> partes inteiras
    /// onde a última absorve o resto para garantir soma exata.
    /// </summary>
    private static List<int> SplitIntoNParts(int total, int n)
    {
        var baseValue = total / n;
        var parts = Enumerable.Repeat(baseValue, n).ToList();
        parts[n - 1] += total - (baseValue * n); // resto para o último
        return parts;
    }
}
