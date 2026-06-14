using FluentAssertions;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="Period"/>.
///
/// Invariantes verificadas:
/// - <c>from &lt;= to</c>; violação lança exceção de domínio.
/// - Janela de 0 a 12 meses aceita no caminho de SLO.
/// - Imutabilidade após construção.
///
/// Mapeia: TASK-03, design §4.3, RNF 1.
/// </summary>
public sealed class PeriodTests
{
    // -------------------------------------------------------------------------
    // Construção válida
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Period com from == to deve ser válido (janela zero dias)")]
    public void Period_WithFromEqualTo_ShouldBeValid()
    {
        var date = new DateOnly(2026, 1, 1);
        var act = () => Period.Create(date, date);

        act.Should().NotThrow();
    }

    [Fact(DisplayName = "Period com from < to deve ser válido")]
    public void Period_WithFromBeforeTo_ShouldBeValid()
    {
        var from = new DateOnly(2026, 1, 1);
        var to   = new DateOnly(2026, 6, 30);

        var period = Period.Create(from, to);

        period.From.Should().Be(from);
        period.To.Should().Be(to);
    }

    [Fact(DisplayName = "Period de 12 meses exatos deve ser válido no caminho de SLO (RNF 1)")]
    public void Period_Of12Months_ShouldBeValid()
    {
        var from = new DateOnly(2026, 1, 1);
        var to   = new DateOnly(2026, 12, 31);

        var act = () => Period.Create(from, to);

        act.Should().NotThrow();
    }

    // -------------------------------------------------------------------------
    // Rejeição de invariantes
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Period com from > to deve lançar exceção de domínio")]
    public void Period_WithFromAfterTo_ShouldThrow()
    {
        var from = new DateOnly(2026, 6, 1);
        var to   = new DateOnly(2026, 1, 1);

        var act = () => Period.Create(from, to);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*from*");
    }

    // -------------------------------------------------------------------------
    // Imutabilidade
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Period deve ser imutável após construção")]
    public void Period_ShouldBeImmutable()
    {
        var from = new DateOnly(2026, 1, 1);
        var to   = new DateOnly(2026, 12, 31);
        var period = Period.Create(from, to);

        // record: igualdade estrutural
        var copy = period with { };
        copy.From.Should().Be(period.From);
        copy.To.Should().Be(period.To);
    }

    [Fact(DisplayName = "Period com mesmas datas deve ser igual por valor")]
    public void Period_WithSameDates_ShouldBeEqualByValue()
    {
        var from = new DateOnly(2026, 3, 1);
        var to   = new DateOnly(2026, 9, 30);

        var a = Period.Create(from, to);
        var b = Period.Create(from, to);

        a.Should().Be(b);
    }

    [Fact(DisplayName = "Period com datas diferentes deve ser diferente")]
    public void Period_WithDifferentDates_ShouldNotBeEqual()
    {
        var a = Period.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        var b = Period.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        a.Should().NotBe(b);
    }
}
