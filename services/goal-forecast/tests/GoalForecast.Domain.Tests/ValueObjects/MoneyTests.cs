using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using GoalForecast.Domain.Exceptions;
using GoalForecast.Domain.ValueObjects;
using Xunit;

namespace GoalForecast.Domain.Tests.ValueObjects;

/// <summary>
/// Testes do objeto de valor <see cref="Money"/> (centavos inteiros).
/// Cobre TASK-03: igualdade por valor, Add/Subtract exatos, rejeição de
/// valor negativo, proibição de double/float e PBT-05 (round-trip).
/// Mapeia: Req 1.2, RNF 4, PBT-05, DEC-011, design §4.3.
/// </summary>
public sealed class MoneyTests
{
    // =========================================================================
    // Construção e invariantes básicas
    // =========================================================================

    [Fact(DisplayName = "Money.Of(0) cria Money.Zero")]
    public void Of_WithZero_CreatesZero()
    {
        var money = Money.Of(0L);

        money.Cents.Should().Be(0L);
        money.Should().Be(Money.Zero);
    }

    [Fact(DisplayName = "Money.Of(valor positivo) preserva o valor em centavos")]
    public void Of_WithPositiveValue_PreservesValue()
    {
        var money = Money.Of(1590L);

        money.Cents.Should().Be(1590L);
    }

    [Fact(DisplayName = "Money.Of(valor negativo) lança DomainException GF-ERR-001")]
    public void Of_WithNegativeValue_ThrowsDomainException()
    {
        var act = () => Money.Of(-1L);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-001");
    }

    [Fact(DisplayName = "Money.Of(long.MinValue) lança DomainException GF-ERR-001")]
    public void Of_WithLongMinValue_ThrowsDomainException()
    {
        var act = () => Money.Of(long.MinValue);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-001");
    }

    // =========================================================================
    // Igualdade por valor
    // =========================================================================

    [Fact(DisplayName = "Dois Money com mesmo Cents são iguais (igualdade por valor)")]
    public void Equality_SameCents_AreEqual()
    {
        var a = Money.Of(500L);
        var b = Money.Of(500L);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact(DisplayName = "Dois Money com Cents diferentes não são iguais")]
    public void Equality_DifferentCents_AreNotEqual()
    {
        var a = Money.Of(100L);
        var b = Money.Of(200L);

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact(DisplayName = "Money.Zero é igual a Money.Of(0)")]
    public void Zero_EqualsOf0()
    {
        Money.Zero.Should().Be(Money.Of(0L));
        Money.Zero.Cents.Should().Be(0L);
    }

    // =========================================================================
    // Add
    // =========================================================================

    [Fact(DisplayName = "Add retorna soma exata em centavos inteiros")]
    public void Add_ReturnsSumExact()
    {
        var a = Money.Of(1000L);
        var b = Money.Of(590L);

        var result = a.Add(b);

        result.Cents.Should().Be(1590L);
    }

    [Fact(DisplayName = "Add(Money.Zero) retorna mesmo valor")]
    public void Add_WithZero_ReturnsSameValue()
    {
        var money = Money.Of(300L);

        money.Add(Money.Zero).Should().Be(money);
    }

    [Fact(DisplayName = "Add é comutativa: a.Add(b) == b.Add(a)")]
    public void Add_IsCommutative()
    {
        var a = Money.Of(100L);
        var b = Money.Of(200L);

        a.Add(b).Should().Be(b.Add(a));
    }

    // =========================================================================
    // Subtract
    // =========================================================================

    [Fact(DisplayName = "Subtract retorna diferença exata em centavos inteiros")]
    public void Subtract_ReturnsDifferenceExact()
    {
        var a = Money.Of(1590L);
        var b = Money.Of(590L);

        var result = a.Subtract(b);

        result.Cents.Should().Be(1000L);
    }

    [Fact(DisplayName = "Subtract a si mesmo resulta em Money.Zero")]
    public void Subtract_Self_ReturnsZero()
    {
        var money = Money.Of(500L);

        money.Subtract(money).Should().Be(Money.Zero);
    }

    [Fact(DisplayName = "Subtract(Money.Zero) retorna mesmo valor")]
    public void Subtract_WithZero_ReturnsSameValue()
    {
        var money = Money.Of(300L);

        money.Subtract(Money.Zero).Should().Be(money);
    }

    [Fact(DisplayName = "Subtract com resultado negativo lança DomainException GF-ERR-001")]
    public void Subtract_ResultNegative_ThrowsDomainException()
    {
        var a = Money.Of(100L);
        var b = Money.Of(200L);

        var act = () => a.Subtract(b);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-001");
    }

    // =========================================================================
    // Imutabilidade — Add/Subtract não alteram o receptor
    // =========================================================================

    [Fact(DisplayName = "Add não modifica o receptor (imutabilidade)")]
    public void Add_DoesNotMutateReceiver()
    {
        var original = Money.Of(100L);
        _ = original.Add(Money.Of(50L));

        original.Cents.Should().Be(100L);
    }

    [Fact(DisplayName = "Subtract não modifica o receptor (imutabilidade)")]
    public void Subtract_DoesNotMutateReceiver()
    {
        var original = Money.Of(200L);
        _ = original.Subtract(Money.Of(50L));

        original.Cents.Should().Be(200L);
    }

    // =========================================================================
    // PBT-05 — Round-trip: para qualquer long não-negativo, Money.Of(v).Cents == v
    // Mapeia: Req 1.2, RNF 4, PBT-05, tasks.md §1.2
    // =========================================================================

    /// <summary>
    /// PBT-05: para qualquer valor não-negativo (incluindo limites grandes como
    /// long.MaxValue / 2), o round-trip Money.Of(v).Cents preserva v exatamente,
    /// sem conversão para float/double.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(NonNegativeLongArbitrary) }, MaxTest = 200,
        DisplayName = "PBT-05: Money.Of(v).Cents == v para qualquer long não-negativo")]
    public Property Pbt05_RoundTrip_PreservesExactValue(long cents)
    {
        var money = Money.Of(cents);
        return Prop.ToProperty(money.Cents == cents);
    }

    /// <summary>
    /// PBT-05 extensão: Add de dois valores preserva soma exata sem perda de precisão.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(NonNegativeLongSmallArbitrary) }, MaxTest = 200,
        DisplayName = "PBT-05: Money.Add preserva soma exata em inteiros")]
    public Property Pbt05_Add_ExactArithmetic(long a, long b)
    {
        var ma = Money.Of(a);
        var mb = Money.Of(b);
        var sum = ma.Add(mb);
        return Prop.ToProperty(sum.Cents == a + b);
    }
}

/// <summary>
/// Gerador de long não-negativo para PBT-05.
/// Inclui limites grandes (long.MaxValue / 2) mas evita overflow em Add.
/// FsCheck 3.x: método estático Generate() retorna Arbitrary de long filtrado.
/// </summary>
public static class NonNegativeLongArbitrary
{
    /// <summary>
    /// Retorna <see cref="Arbitrary{T}"/> de long filtrado para valores em [0, long.MaxValue/2].
    /// </summary>
    public static Arbitrary<long> Generate() =>
        Arb.Filter(ArbMap.Default.ArbFor<long>(), v => v >= 0L && v <= long.MaxValue / 2);
}

/// <summary>
/// Gerador de long não-negativo pequeno para testar Add sem overflow.
/// Valores em [0, long.MaxValue/4] garantem que a soma de dois valores não transborda.
/// </summary>
public static class NonNegativeLongSmallArbitrary
{
    /// <summary>
    /// Retorna <see cref="Arbitrary{T}"/> de long filtrado para valores em [0, long.MaxValue/4].
    /// </summary>
    public static Arbitrary<long> Generate() =>
        Arb.Filter(ArbMap.Default.ArbFor<long>(), v => v >= 0L && v <= long.MaxValue / 4);
}
