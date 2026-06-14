using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Tests.Opportunities.ValueObjects;

/// <summary>
/// Testes para NbrRounding — arredondamento NBR 5891 (ToEven).
/// Mapeia: RNF 11, TASK-02, PBT-03, PBT-04.
/// </summary>
public sealed class NbrRoundingTests
{
    // Casos de aresta ToEven definidos pela NBR 5891 / banker's rounding
    [Theory(DisplayName = "NbrRounding: arredondamento ToEven nos casos de aresta")]
    [InlineData(150, 100, 2)]   // 1,5 → 2 (par)
    [InlineData(250, 100, 2)]   // 2,5 → 2 (par)
    [InlineData(350, 100, 4)]   // 3,5 → 4 (par)
    [InlineData(450, 100, 4)]   // 4,5 → 4 (par)
    [InlineData(550, 100, 6)]   // 5,5 → 6 (par)
    [InlineData(0, 100, 0)]     // 0 → 0
    [InlineData(1000, 3, 333)]  // 333,33... → 333
    [InlineData(1001, 3, 334)]  // 333,66... → 334
    public void RoundHalfToEven_EdgeCases(long numerator, long denominator, long expected)
    {
        var result = NbrRounding.RoundHalfToEven(numerator, denominator);
        result.Should().Be(expected,
            because: $"NbrRounding({numerator}/{denominator}) deve ser {expected} (NBR 5891 ToEven)");
    }

    [Fact(DisplayName = "NbrRounding: resultado igual ao Math.Round ToEven do .NET para inteiros")]
    public void RoundHalfToEven_MatchesDotNetMathRound()
    {
        for (long num = 0; num <= 1000; num += 7)
        {
            for (long den = 1; den <= 100; den += 11)
            {
                var expected = (long)Math.Round((decimal)num / den, MidpointRounding.ToEven);
                var actual = NbrRounding.RoundHalfToEven(num, den);
                actual.Should().Be(expected,
                    because: $"NbrRounding({num}/{den}) deve ser {expected}");
            }
        }
    }

    [Fact(DisplayName = "NbrRounding: denominador zero lança ArgumentException")]
    public void RoundHalfToEven_ZeroDenominator_ThrowsArgumentException()
    {
        var act = () => NbrRounding.RoundHalfToEven(100, 0);
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// PBT-04 (base): forecast = round(valor_total × prob / 100) está em [0, valor_total].
    /// Mínimo 200 amostras FsCheck.
    /// </summary>
    [Property(MaxTest = 200, DisplayName = "PBT-04 (base): NbrRounding — forecast ≥ 0 e ≤ valor_total")]
    public Property PBT04_Forecast_WithinBounds()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, 100_000_000).Select(x => (long)x)),
            Arb.From(Gen.Choose(0, 100).Select(x => (long)x)),
            (valorTotal, probabilidade) =>
            {
                var forecast = NbrRounding.RoundHalfToEven(valorTotal * probabilidade, 100);
                return forecast >= 0 && forecast <= valorTotal;
            });
    }
}
