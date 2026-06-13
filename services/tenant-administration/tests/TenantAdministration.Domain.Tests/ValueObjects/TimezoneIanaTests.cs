using FluentAssertions;
using TenantAdministration.Domain.ValueObjects;
using Xunit;

namespace TenantAdministration.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor TimezoneIana.
/// Cobre TASK-02.
/// </summary>
public sealed class TimezoneIanaTests
{
    [Theory]
    [InlineData("America/Sao_Paulo")]
    [InlineData("UTC")]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    [InlineData("Asia/Tokyo")]
    public void Create_ValidIanaTimezone_ReturnsSuccess(string iana)
    {
        var result = TimezoneIana.Create(iana);
        result.IsSuccess.Should().BeTrue(because: $"'{iana}' é um fuso IANA válido");
        result.Value.Value.Should().Be(iana);
    }

    [Theory]
    [InlineData("Invalid/Zone")]
    [InlineData("BRT")]
    [InlineData("GMT+3")]
    [InlineData("America")]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_InvalidIanaTimezone_ReturnsFailureWithTaErr006(string iana)
    {
        var result = TimezoneIana.Create(iana);
        result.IsFailure.Should().BeTrue(because: $"'{iana}' não é um fuso IANA reconhecido");
        result.ErrorCode.Should().Be("TA-ERR-006");
    }

    [Fact]
    public void Create_NullInput_ReturnsFailure()
    {
        var result = TimezoneIana.Create(null!);
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("TA-ERR-006");
    }

    [Fact]
    public void Default_IsAmericaSaoPaulo()
    {
        TimezoneIana.Default.Value.Should().Be("America/Sao_Paulo");
    }

    [Fact]
    public void TimezoneIana_EqualityByValue()
    {
        var a = TimezoneIana.Create("UTC").Value;
        var b = TimezoneIana.Create("UTC").Value;
        a.Should().Be(b);
    }

    [Fact]
    public void TimezoneIana_HasNoPublicSetter()
    {
        var valueProperty = typeof(TimezoneIana).GetProperty(nameof(TimezoneIana.Value));
        valueProperty.Should().NotBeNull();
        valueProperty!.CanWrite.Should().BeFalse();
    }
}
