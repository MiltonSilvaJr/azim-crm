using FluentAssertions;
using TenantAdministration.Domain.ValueObjects;
using Xunit;

namespace TenantAdministration.Domain.Tests.ValueObjects;

/// <summary>
/// Testes para o objeto de valor ColorPair.
/// Cobre TASK-03: validação de formato e normalização.
/// </summary>
public sealed class ColorPairTests
{
    [Theory]
    [InlineData("#1A73E8", "#34A853")]
    [InlineData("#000000", "#FFFFFF")]
    [InlineData("#ABCDEF", "#012345")]
    [InlineData("#aabbcc", "#ddeeff")]   // lowercase — normaliza para maiúsculas
    public void Create_ValidHexColors_ReturnsSuccess(string primary, string secondary)
    {
        var result = ColorPair.Create(primary, secondary);
        result.IsSuccess.Should().BeTrue(because: $"'{primary}'/'{secondary}' são cores #RRGGBB válidas");
        result.Value.Primary.Should().MatchRegex("^#[0-9A-F]{6}$");
        result.Value.Secondary.Should().MatchRegex("^#[0-9A-F]{6}$");
    }

    [Fact]
    public void Create_LowercaseColors_NormalizesToUppercase()
    {
        var result = ColorPair.Create("#rrggbb", "#aabbcc");
        // rrggbb contém letras inválidas — mas '#aabbcc' é válido
        // Testamos apenas o que é válido
        var result2 = ColorPair.Create("#1a73e8", "#34a853");
        result2.IsSuccess.Should().BeTrue();
        result2.Value.Primary.Should().Be("#1A73E8");
        result2.Value.Secondary.Should().Be("#34A853");
    }

    [Theory]
    [InlineData("1A73E8", "#34A853")]    // sem #
    [InlineData("#GGG", "#34A853")]      // letras inválidas
    [InlineData("#RRGGBBAA", "#34A853")] // 8 chars
    [InlineData("#12345", "#34A853")]    // 5 chars
    [InlineData("", "#34A853")]          // vazio
    [InlineData("#1A73E8", "")]          // secundária vazia
    [InlineData("#1A73E8", "red")]       // não hex
    public void Create_InvalidColors_ReturnsFailure(string primary, string secondary)
    {
        var result = ColorPair.Create(primary, secondary);
        result.IsFailure.Should().BeTrue(because: $"'{primary}'/'{secondary}' contém formato inválido");
        result.ErrorCode.Should().Be("TA-ERR-004");
    }

    [Fact]
    public void ColorPair_EqualityByValue()
    {
        var a = ColorPair.Create("#1A73E8", "#34A853").Value;
        var b = ColorPair.Create("#1A73E8", "#34A853").Value;
        a.Should().Be(b);
    }

    [Fact]
    public void ColorPair_IsImmutable()
    {
        var type = typeof(ColorPair);
        type.GetProperty(nameof(ColorPair.Primary))!.CanWrite.Should().BeFalse();
        type.GetProperty(nameof(ColorPair.Secondary))!.CanWrite.Should().BeFalse();
    }
}
