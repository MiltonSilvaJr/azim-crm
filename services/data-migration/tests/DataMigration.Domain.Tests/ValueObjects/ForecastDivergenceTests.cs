using DataMigration.Domain.ValueObjects;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataMigration.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários do objeto de valor <see cref="ForecastDivergence"/>.
///
/// Cobre: TASK-05, Req 2.3, PBT-06, design §4.3 §4.6, DD-005.
/// Regra: forecast = round_half_even(valorTotal × probabilidade / 100) em centavos.
/// Divergência listada somente quando |Δ| > 1 centavo.
/// Proibição de float/double; apenas aritmética inteira.
/// </summary>
public sealed class ForecastDivergenceTests
{
    // =========================================================================
    // Casos determinísticos de cálculo
    // =========================================================================

    [Theory(DisplayName = "Calculate deve retornar forecastCalculado correto com round_half_even")]
    // valorTotal (centavos), probabilidade (%), forecastPlanilha (centavos), forecastEsperado
    [InlineData(10_000L, 50,  5_000L, 5_000L)]   // R$ 100 × 50% = R$ 50,00 — exato
    [InlineData(10_001L, 50,  5_000L, 5_000L)]   // 10001 × 50 / 100 = 5000,5 → ToEven = 5000 (par próx)
    [InlineData(10_003L, 50,  5_001L, 5_002L)]   // 10003 × 50 / 100 = 5001,5 → ToEven = 5002 (par próx)
    [InlineData(33_333L, 33,  11_000L, 11_000L)] // 33333 × 33 / 100 = 10999,89 → 11000
    [InlineData(0L,       0,      0L,     0L)]   // zero × zero = zero
    [InlineData(1L,     100,      1L,     1L)]   // 1 × 100 / 100 = 1
    public void Calculate_ShouldReturnCorrectForecast(
        long valorTotal, int probabilidade, long forecastPlanilha, long forecastEsperado)
    {
        var result = ForecastDivergence.Calculate(valorTotal, probabilidade, forecastPlanilha);
        result.ForecastCalculado.Should().Be(forecastEsperado);
    }

    // =========================================================================
    // Arredondamento bancário (round_half_even / NBR 5891)
    // =========================================================================

    [Fact(DisplayName = "Arredondamento half-even: 0,5 centavo arredonda para par (caso 2,5 → 2)")]
    public void Rounding_HalfEven_RoundsToEven_Case25To2()
    {
        // 5 × 50 / 100 = 2,5 centavos → round_half_even = 2 (par)
        var result = ForecastDivergence.Calculate(
            valorTotal: 5L,
            probabilidade: 50,
            forecastPlanilha: 2L);
        result.ForecastCalculado.Should().Be(2L);
    }

    [Fact(DisplayName = "Arredondamento half-even: 1,5 centavo arredonda para par (caso 3,5 → 4)")]
    public void Rounding_HalfEven_RoundsToEven_Case35To4()
    {
        // 7 × 50 / 100 = 3,5 centavos → round_half_even = 4 (par)
        var result = ForecastDivergence.Calculate(
            valorTotal: 7L,
            probabilidade: 50,
            forecastPlanilha: 3L);
        result.ForecastCalculado.Should().Be(4L);
    }

    [Fact(DisplayName = "Arredondamento half-even: 4,5 centavos → 4 (par próximo)")]
    public void Rounding_HalfEven_Case45To4()
    {
        // 9 × 50 / 100 = 4,5 centavos → round_half_even = 4 (par)
        var result = ForecastDivergence.Calculate(
            valorTotal: 9L,
            probabilidade: 50,
            forecastPlanilha: 5L);
        result.ForecastCalculado.Should().Be(4L);
    }

    // =========================================================================
    // HasDivergence (threshold = 1 centavo)
    // =========================================================================

    [Fact(DisplayName = "HasDivergence deve ser false quando |Δ| == 0")]
    public void HasDivergence_ShouldBeFalse_WhenDeltaIsZero()
    {
        var result = ForecastDivergence.Calculate(
            valorTotal: 10_000L,
            probabilidade: 50,
            forecastPlanilha: 5_000L);
        result.HasDivergence.Should().BeFalse();
        result.Delta.Should().Be(0L);
    }

    [Fact(DisplayName = "HasDivergence deve ser false quando |Δ| == 1 (threshold exclusivo)")]
    public void HasDivergence_ShouldBeFalse_WhenDeltaIsOne()
    {
        // forecastCalculado = 5000, forecastPlanilha = 5001 → |Δ| = 1 → não diverge
        var result = ForecastDivergence.Calculate(
            valorTotal: 10_000L,
            probabilidade: 50,
            forecastPlanilha: 5_001L);
        result.HasDivergence.Should().BeFalse();
        result.Delta.Abs().Should().Be(1L);
    }

    [Fact(DisplayName = "HasDivergence deve ser true quando |Δ| == 2 (acima do threshold)")]
    public void HasDivergence_ShouldBeTrue_WhenDeltaIsTwo()
    {
        // forecastCalculado = 5000, forecastPlanilha = 5002 → |Δ| = 2 → diverge
        var result = ForecastDivergence.Calculate(
            valorTotal: 10_000L,
            probabilidade: 50,
            forecastPlanilha: 5_002L);
        result.HasDivergence.Should().BeTrue();
        result.Delta.Abs().Should().Be(2L);
    }

    [Fact(DisplayName = "HasDivergence deve ser true quando forecastPlanilha muito menor")]
    public void HasDivergence_ShouldBeTrue_WhenPlanilhaMuchSmaller()
    {
        var result = ForecastDivergence.Calculate(
            valorTotal: 100_000L,
            probabilidade: 75,
            forecastPlanilha: 50_000L);
        result.HasDivergence.Should().BeTrue();
    }

    // =========================================================================
    // Igualdade por valor
    // =========================================================================

    [Fact(DisplayName = "ForecastDivergence com mesmos valores deve ser igual por valor")]
    public void ForecastDivergence_SameValues_ShouldBeEqual()
    {
        var a = ForecastDivergence.Calculate(10_000L, 50, 5_000L);
        var b = ForecastDivergence.Calculate(10_000L, 50, 5_000L);
        a.Should().Be(b);
    }

    [Fact(DisplayName = "ForecastDivergence com forecastPlanilha diferente deve ser diferente")]
    public void ForecastDivergence_DifferentPlanilha_ShouldNotBeEqual()
    {
        var a = ForecastDivergence.Calculate(10_000L, 50, 5_000L);
        var b = ForecastDivergence.Calculate(10_000L, 50, 6_000L);
        a.Should().NotBe(b);
    }

    // =========================================================================
    // Rejeição de argumentos inválidos
    // =========================================================================

    [Fact(DisplayName = "Calculate deve rejeitar valorTotal negativo")]
    public void Calculate_ShouldReject_NegativeValorTotal()
    {
        var act = () => ForecastDivergence.Calculate(-1L, 50, 0L);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Calculate deve rejeitar probabilidade fora de [0, 100]")]
    public void Calculate_ShouldReject_ProbabilidadeOutOfRange()
    {
        var act1 = () => ForecastDivergence.Calculate(1000L, -1, 0L);
        var act2 = () => ForecastDivergence.Calculate(1000L, 101, 0L);
        act1.Should().Throw<ArgumentOutOfRangeException>();
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    // =========================================================================
    // PBT-06 — FsCheck
    // Propriedade: para qualquer valorTotal e probabilidade,
    //   forecastCalculado = round_half_even(valorTotal × probabilidade / 100)
    //   hasDivergence = |forecastCalculado - forecastPlanilha| > 1
    // Mapeia: PBT-06, design §4.3 §4.6, DD-005, TASK-05.
    // =========================================================================

    /// <summary>
    /// PBT-06 (parte 1): o forecast calculado corresponde a round_half_even aplicado em centavos.
    /// Verifica que o resultado é igual ao cálculo de referência com <c>decimal + MidpointRounding.ToEven</c>.
    ///
    /// Rastreia: PBT-06, design §13.
    /// </summary>
    [Property(
        DisplayName = "PBT-06: ForecastCalculado == round_half_even(valorTotal × probabilidade / 100)",
        MaxTest = 200)]
    public Property Pbt06_ForecastCalculado_MatchesRoundHalfEven(
        NonNegativeInt rawTotal, byte rawProb)
    {
        var valorTotal = (long)rawTotal.Get;
        var probabilidade = (int)(rawProb % 101); // 0..100

        var result = ForecastDivergence.Calculate(valorTotal, probabilidade, forecastPlanilha: 0L);

        // Referência: decimal com MidpointRounding.ToEven (NBR 5891)
        var expected = (long)Math.Round(
            (decimal)valorTotal * probabilidade / 100m,
            MidpointRounding.ToEven);

        return Prop.Label(
            result.ForecastCalculado == expected,
            $"valorTotal={valorTotal}, prob={probabilidade}, " +
            $"calculado={result.ForecastCalculado}, esperado={expected}");
    }

    /// <summary>
    /// PBT-06 (parte 2): hasDivergence é verdadeiro se e somente se |Δ| > 1.
    ///
    /// Rastreia: PBT-06, design §4.3, DD-005.
    /// </summary>
    [Property(
        DisplayName = "PBT-06: HasDivergence = |forecastCalculado - forecastPlanilha| > 1",
        MaxTest = 200)]
    public Property Pbt06_HasDivergence_OnlyWhenDeltaGtOne(
        NonNegativeInt rawTotal, byte rawProb, NonNegativeInt rawPlanilha)
    {
        var valorTotal = (long)rawTotal.Get;
        var probabilidade = (int)(rawProb % 101);
        var forecastPlanilha = (long)rawPlanilha.Get;

        var result = ForecastDivergence.Calculate(valorTotal, probabilidade, forecastPlanilha);
        var expectedDelta = Math.Abs(result.ForecastCalculado - forecastPlanilha);
        var expectedHasDivergence = expectedDelta > ForecastDivergence.DivergenceThresholdCents;

        return Prop.Label(
            result.HasDivergence == expectedHasDivergence,
            $"calculado={result.ForecastCalculado}, planilha={forecastPlanilha}, " +
            $"|Δ|={expectedDelta}, hasDivergence={result.HasDivergence}");
    }
}

/// <summary>
/// Extensão auxiliar para valor absoluto de long (sem ambiguidade de Math.Abs).
/// </summary>
internal static class LongExtensions
{
    public static long Abs(this long value) => value < 0 ? -value : value;
}
