using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using GoalForecast.Domain.Exceptions;
using GoalForecast.Domain.ValueObjects;
using Xunit;

namespace GoalForecast.Domain.Tests.ValueObjects;

/// <summary>
/// Testes do objeto de valor <see cref="Money"/> (centavos inteiros + moeda ISO-4217).
/// Cobre TASK-03: igualdade por valor, Add/Subtract exatos, rejeição de
/// valor negativo, validação de moeda (ADR-0008), mismatch de moeda e PBT-05 (round-trip).
/// Mapeia: Req 1.2, RNF 4, PBT-05, DEC-011, ADR-0008, design §4.3.
/// </summary>
public sealed class MoneyTests
{
    // =========================================================================
    // Construção e invariantes básicas
    // =========================================================================

    [Fact(DisplayName = "Money.Of(0) cria Money.Zero em BRL")]
    public void Of_WithZero_CreatesZero()
    {
        var money = Money.Of(0L);

        money.Cents.Should().Be(0L);
        money.Currency.Should().Be("BRL");
        money.Should().Be(Money.Zero);
    }

    [Fact(DisplayName = "Money.Of(valor positivo) preserva o valor em centavos")]
    public void Of_WithPositiveValue_PreservesValue()
    {
        var money = Money.Of(1590L);

        money.Cents.Should().Be(1590L);
        money.Currency.Should().Be("BRL");
    }

    [Fact(DisplayName = "Money.Of(valor, 'USD') cria Money em USD (ADR-0008)")]
    public void Of_WithCurrency_CreatesMoneyInGivenCurrency()
    {
        var money = Money.Of(5000L, "USD");

        money.Cents.Should().Be(5000L);
        money.Currency.Should().Be("USD");
    }

    [Fact(DisplayName = "Money.Of(valor, 'EUR') cria Money em EUR (ADR-0008)")]
    public void Of_WithEur_CreatesMoneyInEur()
    {
        var money = Money.Of(9900L, "EUR");

        money.Cents.Should().Be(9900L);
        money.Currency.Should().Be("EUR");
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

    [Fact(DisplayName = "Money.Of(valor, moeda inválida) lança DomainException GF-ERR-001 (ADR-0008)")]
    public void Of_WithInvalidCurrency_ThrowsDomainException()
    {
        var act = () => Money.Of(100L, "GBP");

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-001");
    }

    [Fact(DisplayName = "Money.Of(valor, null) lança DomainException GF-ERR-001 (ADR-0008)")]
    public void Of_WithNullCurrency_ThrowsDomainException()
    {
        var act = () => Money.Of(100L, null!);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-001");
    }

    [Fact(DisplayName = "Money.Of(valor, string vazia) lança DomainException GF-ERR-001 (ADR-0008)")]
    public void Of_WithEmptyCurrency_ThrowsDomainException()
    {
        var act = () => Money.Of(100L, "");

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-001");
    }

    // =========================================================================
    // Zero por moeda (ADR-0008)
    // =========================================================================

    [Fact(DisplayName = "Money.Zero é BRL e Cents=0 (retrocompatibilidade)")]
    public void Zero_IsBrlAndZeroCents()
    {
        Money.Zero.Cents.Should().Be(0L);
        Money.Zero.Currency.Should().Be("BRL");
    }

    [Fact(DisplayName = "Money.ZeroIn('USD') cria zero em USD (ADR-0008)")]
    public void Zero_WithUsd_CreatesZeroInUsd()
    {
        var z = Money.ZeroIn("USD");

        z.Cents.Should().Be(0L);
        z.Currency.Should().Be("USD");
    }

    [Fact(DisplayName = "Money.ZeroIn('EUR') cria zero em EUR (ADR-0008)")]
    public void Zero_WithEur_CreatesZeroInEur()
    {
        var z = Money.ZeroIn("EUR");

        z.Cents.Should().Be(0L);
        z.Currency.Should().Be("EUR");
    }

    [Fact(DisplayName = "Money.Zero é igual a Money.Of(0) em BRL")]
    public void Zero_EqualsOf0()
    {
        Money.Zero.Should().Be(Money.Of(0L));
        Money.Zero.Cents.Should().Be(0L);
    }

    // =========================================================================
    // Igualdade por valor
    // =========================================================================

    [Fact(DisplayName = "Dois Money com mesmo Cents e Currency são iguais (igualdade por valor)")]
    public void Equality_SameCentsAndCurrency_AreEqual()
    {
        var a = Money.Of(500L, "BRL");
        var b = Money.Of(500L, "BRL");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact(DisplayName = "Money BRL e USD com mesmo Cents não são iguais (ADR-0008)")]
    public void Equality_SameCentsDifferentCurrency_AreNotEqual()
    {
        var a = Money.Of(500L, "BRL");
        var b = Money.Of(500L, "USD");

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact(DisplayName = "Dois Money com Cents diferentes não são iguais")]
    public void Equality_DifferentCents_AreNotEqual()
    {
        var a = Money.Of(100L);
        var b = Money.Of(200L);

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    // =========================================================================
    // Add (mesma moeda)
    // =========================================================================

    [Fact(DisplayName = "Add retorna soma exata em centavos inteiros")]
    public void Add_ReturnsSumExact()
    {
        var a = Money.Of(1000L);
        var b = Money.Of(590L);

        var result = a.Add(b);

        result.Cents.Should().Be(1590L);
        result.Currency.Should().Be("BRL");
    }

    [Fact(DisplayName = "Add USD+USD retorna soma em USD (ADR-0008)")]
    public void Add_UsdPlusUsd_ReturnsSumInUsd()
    {
        var a = Money.Of(300L, "USD");
        var b = Money.Of(200L, "USD");

        var result = a.Add(b);

        result.Cents.Should().Be(500L);
        result.Currency.Should().Be("USD");
    }

    [Fact(DisplayName = "Add BRL+USD lança DomainException GF-ERR-001 (ADR-0008 mismatch)")]
    public void Add_BrlPlusUsd_ThrowsDomainException()
    {
        var a = Money.Of(100L, "BRL");
        var b = Money.Of(100L, "USD");

        var act = () => a.Add(b);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-001");
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
    // Subtract (mesma moeda)
    // =========================================================================

    [Fact(DisplayName = "Subtract retorna diferença exata em centavos inteiros")]
    public void Subtract_ReturnsDifferenceExact()
    {
        var a = Money.Of(1590L);
        var b = Money.Of(590L);

        var result = a.Subtract(b);

        result.Cents.Should().Be(1000L);
        result.Currency.Should().Be("BRL");
    }

    [Fact(DisplayName = "Subtract BRL-USD lança DomainException GF-ERR-001 (ADR-0008 mismatch)")]
    public void Subtract_BrlMinusUsd_ThrowsDomainException()
    {
        var a = Money.Of(500L, "BRL");
        var b = Money.Of(100L, "USD");

        var act = () => a.Subtract(b);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-001");
    }

    [Fact(DisplayName = "Subtract a si mesmo resulta em Money.Zero na mesma moeda")]
    public void Subtract_Self_ReturnsZeroInSameCurrency()
    {
        var money = Money.Of(500L, "USD");

        var result = money.Subtract(money);

        result.Cents.Should().Be(0L);
        result.Currency.Should().Be("USD");
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
        original.Currency.Should().Be("BRL");
    }

    [Fact(DisplayName = "Subtract não modifica o receptor (imutabilidade)")]
    public void Subtract_DoesNotMutateReceiver()
    {
        var original = Money.Of(200L);
        _ = original.Subtract(Money.Of(50L));

        original.Cents.Should().Be(200L);
        original.Currency.Should().Be("BRL");
    }

    // =========================================================================
    // SupportedCurrencies (ADR-0008)
    // =========================================================================

    [Theory(DisplayName = "SupportedCurrencies contém BRL, USD e EUR (ADR-0008)")]
    [InlineData("BRL")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void SupportedCurrencies_ContainsMvpCurrencies(string currency)
    {
        Money.SupportedCurrencies.Should().Contain(currency);
    }

    [Theory(DisplayName = "Moedas fora do MVP não são suportadas (ADR-0008)")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    [InlineData("CHF")]
    [InlineData("ARS")]
    public void SupportedCurrencies_DoesNotContainNonMvpCurrencies(string currency)
    {
        Money.SupportedCurrencies.Should().NotContain(currency);
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

    /// <summary>
    /// PBT ADR-0008: Money.Of(v, currency).Currency preserva a moeda exatamente.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(NonNegativeLongSmallArbitrary) }, MaxTest = 100,
        DisplayName = "PBT ADR-0008: Money.Of(v, currency).Currency preserva a moeda")]
    public Property Pbt_CurrencyRoundTrip_BRL(long cents)
    {
        var m = Money.Of(cents, "BRL");
        return Prop.ToProperty(m.Currency == "BRL" && m.Cents == cents);
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
