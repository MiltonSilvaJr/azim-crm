using FluentAssertions;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="StageCategory"/>.
/// Cobre: valores canônicos aceitos, valores inválidos rejeitados.
/// </summary>
public sealed class StageCategoryTests
{
    [Theory]
    [InlineData("open")]
    [InlineData("won")]
    [InlineData("lost")]
    public void Create_WhenValueIsCanonical_ShouldSucceed(string value)
    {
        var result = StageCategory.Create(value);
        result.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("Open")]
    [InlineData("WON")]
    [InlineData("LOST")]
    [InlineData("pending")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("closed")]
    public void Create_WhenValueIsInvalid_ShouldThrow(string value)
    {
        var act = () => StageCategory.Create(value);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WhenValueIsNull_ShouldThrow()
    {
        var act = () => StageCategory.Create(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_WhenSameValue_ShouldBeEqual()
    {
        var a = StageCategory.Create("open");
        var b = StageCategory.Create("open");
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_WhenDifferentValues_ShouldNotBeEqual()
    {
        var a = StageCategory.Create("open");
        var b = StageCategory.Create("won");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Open_StaticProperty_ShouldBeValid()
    {
        StageCategory.Open.Value.Should().Be("open");
    }

    [Fact]
    public void Won_StaticProperty_ShouldBeValid()
    {
        StageCategory.Won.Value.Should().Be("won");
    }

    [Fact]
    public void Lost_StaticProperty_ShouldBeValid()
    {
        StageCategory.Lost.Value.Should().Be("lost");
    }

    [Fact]
    public void IsTerminal_WhenWon_ShouldBeTrue()
    {
        StageCategory.Won.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsTerminal_WhenLost_ShouldBeTrue()
    {
        StageCategory.Lost.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsTerminal_WhenOpen_ShouldBeFalse()
    {
        StageCategory.Open.IsTerminal.Should().BeFalse();
    }
}
