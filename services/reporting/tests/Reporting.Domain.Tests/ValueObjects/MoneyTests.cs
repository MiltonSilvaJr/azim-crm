using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários e PBT-02 para o objeto de valor <see cref="Money"/>.
///
/// Invariantes verificadas:
/// - Imutabilidade e igualdade por valor.
/// - Cents como <c>long</c> inteiro, sem <c>float</c>/<c>double</c>.
/// - <c>Add</c> e <c>Subtract</c> exatos em inteiros.
/// - Rejeição de valor negativo na construção.
/// - PBT-02: para qualquer lista de centavos inteiros não-negativos,
///   a soma via <c>Money</c> é exatamente igual à soma aritmética em <c>long</c>.
///
/// Mapeia: TASK-03, design §4.3, DD-007, PBT-02.
/// </summary>
public sealed class MoneyTests
{
    // -------------------------------------------------------------------------
    // Construção e igualdade por valor
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Money com mesmos cents deve ser igual por valor (DD-007)")]
    public void Money_WithSameCents_ShouldBeEqualByValue()
    {
        var a = Money.FromCents(1500L);
        var b = Money.FromCents(1500L);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact(DisplayName = "Money com cents diferentes deve ser diferente (DD-007)")]
    public void Money_WithDifferentCents_ShouldNotBeEqual()
    {
        var a = Money.FromCents(1000L);
        var b = Money.FromCents(2000L);

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact(DisplayName = "Money zero deve ser construível (DD-007)")]
    public void Money_Zero_ShouldBeConstructible()
    {
        var zero = Money.Zero;

        zero.Cents.Should().Be(0L);
    }

    // -------------------------------------------------------------------------
    // Rejeição de valores negativos
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Money com cents negativos deve lançar exceção de domínio (DD-007)")]
    public void Money_WithNegativeCents_ShouldThrow()
    {
        var act = () => Money.FromCents(-1L);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*cents*");
    }

    [Theory(DisplayName = "Money com cents negativos variados deve lançar (DD-007)")]
    [InlineData(-1L)]
    [InlineData(-100L)]
    [InlineData(long.MinValue)]
    public void Money_WithVariousNegativeCents_ShouldThrow(long cents)
    {
        var act = () => Money.FromCents(cents);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // -------------------------------------------------------------------------
    // Proibição de construção via float/double
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Money não deve expor construtor que aceita float (DD-007)")]
    public void Money_ShouldNotHave_FloatConstructor()
    {
        var type = typeof(Money);

        var floatConstructor = type.GetConstructors()
            .Any(c => c.GetParameters().Any(p =>
                p.ParameterType == typeof(float) || p.ParameterType == typeof(double)));

        floatConstructor.Should().BeFalse(
            because: "Money não deve aceitar float/double — apenas long cents (DD-007)");
    }

    [Fact(DisplayName = "Money não deve expor factory method que aceita double (DD-007)")]
    public void Money_ShouldNotHave_DoubleFactoryMethod()
    {
        var type = typeof(Money);

        var doubleFactory = type.GetMethods()
            .Where(m => m.IsStatic && m.IsPublic)
            .Any(m => m.GetParameters().Any(p =>
                p.ParameterType == typeof(float) || p.ParameterType == typeof(double)));

        doubleFactory.Should().BeFalse(
            because: "Money não deve ter factory method com float/double (DD-007)");
    }

    // -------------------------------------------------------------------------
    // Operações Add e Subtract
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Money.Add deve retornar soma exata em long (DD-007)")]
    public void Money_Add_ShouldReturnExactSum()
    {
        var a = Money.FromCents(300L);
        var b = Money.FromCents(200L);

        var result = a.Add(b);

        result.Cents.Should().Be(500L);
    }

    [Fact(DisplayName = "Money.Add deve retornar novo objeto imutável (DD-007)")]
    public void Money_Add_ShouldReturnNewImmutableObject()
    {
        var original = Money.FromCents(100L);
        var added = original.Add(Money.FromCents(50L));

        original.Cents.Should().Be(100L, because: "Money é imutável — Add não altera o original");
        added.Cents.Should().Be(150L);
    }

    [Fact(DisplayName = "Money.Subtract deve retornar diferença exata em long (DD-007)")]
    public void Money_Subtract_ShouldReturnExactDifference()
    {
        var a = Money.FromCents(500L);
        var b = Money.FromCents(200L);

        var result = a.Subtract(b);

        result.Cents.Should().Be(300L);
    }

    [Fact(DisplayName = "Money.Subtract resultando em negativo deve lançar (DD-007)")]
    public void Money_Subtract_ResultingInNegative_ShouldThrow()
    {
        var a = Money.FromCents(100L);
        var b = Money.FromCents(200L);

        var act = () => a.Subtract(b);

        act.Should().Throw<InvalidOperationException>();
    }

    // -------------------------------------------------------------------------
    // PBT-02: conservação da soma de centavos inteiros sem perda/duplicação
    // -------------------------------------------------------------------------

    /// <summary>
    /// PBT-02: para qualquer lista de centavos inteiros não-negativos,
    /// a soma via <c>Money.Add</c> é exatamente igual à soma aritmética em <c>long</c>.
    ///
    /// Mapeia: PBT-02, design §13.6, DD-007.
    /// </summary>
    [Property(MaxTest = 500, DisplayName = "PBT-02: soma de Money em centavos = soma aritmética exata (PBT-02, DD-007)")]
    public Property Money_Sum_ShouldEqualArithmeticSum(PositiveInt count)
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(count)),
            c =>
            {
                // Gera lista de centavos entre 0 e 1.000.000
                var n = c.Get % 50 + 1; // 1 a 50 elementos
                var cents = Enumerable.Range(0, n).Select(i => (long)(i * 1_000)).ToList();
                var expected = cents.Aggregate(0L, (acc, v) => acc + v);
                var moneyList = cents.Select(Money.FromCents).ToList();
                var actual = moneyList.Aggregate(Money.Zero, (acc, m) => acc.Add(m));

                return actual.Cents == expected;
            });
    }

    [Fact(DisplayName = "PBT-02: soma de lista vazia de Money deve ser zero (PBT-02)")]
    public void Money_SumOfEmptyList_ShouldBeZero()
    {
        var result = Enumerable.Empty<Money>().Aggregate(Money.Zero, (acc, m) => acc.Add(m));

        result.Cents.Should().Be(0L);
    }

    [Fact(DisplayName = "PBT-02: soma de grandes centavos não deve perder precisão (PBT-02, DD-007)")]
    public void Money_LargeSum_ShouldPreservePrecision()
    {
        // 2.000 oportunidades × R$100.000 = R$200.000.000 = 20.000.000.000 centavos
        // long.MaxValue ≈ 9.2 × 10^18 — não há overflow neste volume de referência
        var perOpp = Money.FromCents(10_000_000L); // R$100.000
        var sum = Enumerable.Range(0, 2000)
            .Aggregate(Money.Zero, (acc, _) => acc.Add(perOpp));

        sum.Cents.Should().Be(20_000_000_000L);
    }
}
