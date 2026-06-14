using FluentAssertions;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.Domain.Tests.Enums;

/// <summary>
/// Testes unitários para o enum <see cref="ReportType"/>.
///
/// Invariantes verificadas:
/// - Lista canônica fechada de cinco valores.
/// - Parsing de string inválida deve ser tratado (enum fora do range rejeitado).
///
/// Mapeia: TASK-04, design §4.3, Req 3–6.
/// </summary>
public sealed class ReportTypeTests
{
    [Fact(DisplayName = "ReportType deve ter exatamente 5 valores canônicos (design §4.3)")]
    public void ReportType_ShouldHaveExactlyFiveValues()
    {
        var values = Enum.GetValues<ReportType>();

        values.Should().HaveCount(5,
            because: "ReportType é uma lista canônica fechada: Funnel, Forecast, Ranking, Channel, Commissions");
    }

    [Fact(DisplayName = "ReportType deve conter Funnel (Req 1)")]
    public void ReportType_ShouldContain_Funnel()
    {
        Enum.IsDefined(typeof(ReportType), ReportType.Funnel).Should().BeTrue();
    }

    [Fact(DisplayName = "ReportType deve conter Forecast (Req 6)")]
    public void ReportType_ShouldContain_Forecast()
    {
        Enum.IsDefined(typeof(ReportType), ReportType.Forecast).Should().BeTrue();
    }

    [Fact(DisplayName = "ReportType deve conter Ranking (Req 2)")]
    public void ReportType_ShouldContain_Ranking()
    {
        Enum.IsDefined(typeof(ReportType), ReportType.Ranking).Should().BeTrue();
    }

    [Fact(DisplayName = "ReportType deve conter Channel (Req 3)")]
    public void ReportType_ShouldContain_Channel()
    {
        Enum.IsDefined(typeof(ReportType), ReportType.Channel).Should().BeTrue();
    }

    [Fact(DisplayName = "ReportType deve conter Commissions (Req 4)")]
    public void ReportType_ShouldContain_Commissions()
    {
        Enum.IsDefined(typeof(ReportType), ReportType.Commissions).Should().BeTrue();
    }

    [Fact(DisplayName = "ReportType com valor fora do enum deve ser identificado como indefinido (design §4.3)")]
    public void ReportType_OutOfRange_ShouldBeUndefined()
    {
        var invalid = (ReportType)999;

        Enum.IsDefined(typeof(ReportType), invalid).Should().BeFalse(
            because: "ReportType é lista canônica fechada — valor 999 não pertence ao enum");
    }
}
