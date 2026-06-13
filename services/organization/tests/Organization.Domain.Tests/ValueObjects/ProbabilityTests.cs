using FluentAssertions;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="Probability"/>.
/// Cobre: intervalo 0..100 válido, -1 e 101 rejeitados, igualdade por valor.
/// </summary>
public sealed class ProbabilityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    public void Create_WhenValueIsInRange_ShouldSucceed(int value)
    {
        var result = Probability.Create(value);
        result.Value.Should().Be(value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(101)]
    [InlineData(200)]
    public void Create_WhenValueIsOutOfRange_ShouldThrow(int value)
    {
        var act = () => Probability.Create(value);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_WhenSameValue_ShouldBeEqual()
    {
        var a = Probability.Create(75);
        var b = Probability.Create(75);
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_WhenDifferentValues_ShouldNotBeEqual()
    {
        var a = Probability.Create(25);
        var b = Probability.Create(75);
        a.Should().NotBe(b);
    }

    [Fact]
    public void Zero_StaticProperty_ShouldBeValid()
    {
        Probability.Zero.Value.Should().Be(0);
    }

    [Fact]
    public void Hundred_StaticProperty_ShouldBeValid()
    {
        Probability.Hundred.Value.Should().Be(100);
    }
}
