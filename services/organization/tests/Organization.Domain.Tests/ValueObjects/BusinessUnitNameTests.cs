using FluentAssertions;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="BusinessUnitName"/>.
/// Cobre: rejeição de vazio, trim, comprimento máximo e igualdade insensível a caixa.
/// </summary>
public sealed class BusinessUnitNameTests
{
    [Fact]
    public void Create_WhenValueIsNull_ShouldThrow()
    {
        var act = () => BusinessUnitName.Create(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenValueIsEmpty_ShouldThrow(string value)
    {
        var act = () => BusinessUnitName.Create(value);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WhenValueExceeds120Chars_ShouldThrow()
    {
        var longName = new string('x', 121);
        var act = () => BusinessUnitName.Create(longName);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WhenValueIs120Chars_ShouldSucceed()
    {
        var name = new string('x', 120);
        var result = BusinessUnitName.Create(name);
        result.Value.Should().HaveLength(120);
    }

    [Fact]
    public void Create_WhenValueHasSurroundingWhitespace_ShouldTrim()
    {
        var result = BusinessUnitName.Create("  Vendas  ");
        result.Value.Should().Be("Vendas");
    }

    [Fact]
    public void Equality_WhenSameValueDifferentCase_ShouldBeEqual()
    {
        var a = BusinessUnitName.Create("Vendas");
        var b = BusinessUnitName.Create("VENDAS");
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_WhenDifferentValues_ShouldNotBeEqual()
    {
        var a = BusinessUnitName.Create("Vendas");
        var b = BusinessUnitName.Create("Marketing");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Equality_WhenSameValueSameCase_ShouldBeEqual()
    {
        var a = BusinessUnitName.Create("Vendas");
        var b = BusinessUnitName.Create("Vendas");
        a.Should().Be(b);
    }

    [Fact]
    public void Create_WhenValueIsValid_ShouldStoreNormalizedValue()
    {
        var result = BusinessUnitName.Create("  Suporte  ");
        result.Value.Should().Be("Suporte");
    }
}
