using FluentAssertions;
using GoalForecast.Domain.Exceptions;
using GoalForecast.Domain.ValueObjects;
using Xunit;

namespace GoalForecast.Domain.Tests.ValueObjects;

/// <summary>
/// Testes do objeto de valor <see cref="GoalPeriod"/> (ano de quatro dígitos, mês 1..12).
/// Cobre TASK-04: INV-2, igualdade por valor, Quarter() e YearOf().
/// Mapeia: Req 1.3, requirements §4, INV-2, design §4.3.
/// </summary>
public sealed class GoalPeriodTests
{
    // =========================================================================
    // Construção válida
    // =========================================================================

    [Fact(DisplayName = "GoalPeriod(2026, 6) cria período válido")]
    public void GoalPeriod_ValidYearAndMonth_Creates()
    {
        var period = new GoalPeriod(2026, 6);

        period.Year.Should().Be(2026);
        period.Month.Should().Be(6);
    }

    [Theory(DisplayName = "GoalPeriod aceita meses de 1 a 12")]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    public void GoalPeriod_ValidMonths_Creates(int month)
    {
        var act = () => new GoalPeriod(2026, month);
        act.Should().NotThrow();
    }

    // =========================================================================
    // INV-2: mês fora de [1..12] deve lançar
    // =========================================================================

    [Theory(DisplayName = "GoalPeriod com mês fora de [1..12] lança DomainException GF-ERR-002")]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    [InlineData(100)]
    public void GoalPeriod_InvalidMonth_ThrowsDomainException(int month)
    {
        var act = () => new GoalPeriod(2026, month);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-002");
    }

    // =========================================================================
    // INV-2: ano de dois dígitos deve lançar
    // =========================================================================

    [Theory(DisplayName = "GoalPeriod com ano de dois dígitos lança DomainException GF-ERR-002")]
    [InlineData(26)]
    [InlineData(99)]
    [InlineData(0)]
    [InlineData(999)]
    public void GoalPeriod_YearWithFewerThanFourDigits_ThrowsDomainException(int year)
    {
        var act = () => new GoalPeriod(year, 6);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-002");
    }

    // =========================================================================
    // Igualdade por valor
    // =========================================================================

    [Fact(DisplayName = "Dois GoalPeriod com mesmo year/month são iguais")]
    public void Equality_SameYearAndMonth_AreEqual()
    {
        var a = new GoalPeriod(2026, 6);
        var b = new GoalPeriod(2026, 6);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact(DisplayName = "GoalPeriod com anos diferentes não são iguais")]
    public void Equality_DifferentYear_NotEqual()
    {
        var a = new GoalPeriod(2025, 6);
        var b = new GoalPeriod(2026, 6);

        a.Should().NotBe(b);
    }

    [Fact(DisplayName = "GoalPeriod com meses diferentes não são iguais")]
    public void Equality_DifferentMonth_NotEqual()
    {
        var a = new GoalPeriod(2026, 5);
        var b = new GoalPeriod(2026, 6);

        a.Should().NotBe(b);
    }

    // =========================================================================
    // Quarter()
    // =========================================================================

    [Theory(DisplayName = "Quarter() retorna 1 para meses 1, 2, 3")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Quarter_Q1Months_Returns1(int month)
    {
        new GoalPeriod(2026, month).Quarter().Should().Be(1);
    }

    [Theory(DisplayName = "Quarter() retorna 2 para meses 4, 5, 6")]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Quarter_Q2Months_Returns2(int month)
    {
        new GoalPeriod(2026, month).Quarter().Should().Be(2);
    }

    [Theory(DisplayName = "Quarter() retorna 3 para meses 7, 8, 9")]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Quarter_Q3Months_Returns3(int month)
    {
        new GoalPeriod(2026, month).Quarter().Should().Be(3);
    }

    [Theory(DisplayName = "Quarter() retorna 4 para meses 10, 11, 12")]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void Quarter_Q4Months_Returns4(int month)
    {
        new GoalPeriod(2026, month).Quarter().Should().Be(4);
    }

    // =========================================================================
    // YearOf()
    // =========================================================================

    [Fact(DisplayName = "YearOf() retorna o ano do período")]
    public void YearOf_ReturnsYear()
    {
        var period = new GoalPeriod(2026, 6);

        period.YearOf().Should().Be(2026);
    }
}
