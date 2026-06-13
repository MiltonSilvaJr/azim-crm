using FluentAssertions;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="Role"/>.
/// Cobre: valores canônicos aceitos, valores inválidos rejeitados, PlatOp explicitamente rejeitado.
/// </summary>
public sealed class RoleTests
{
    [Theory]
    [InlineData("TAdmin")]
    [InlineData("GestorBU")]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    public void Create_WhenValueIsCanonical_ShouldSucceed(string value)
    {
        var result = Role.Create(value);
        result.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("PlatOp")]
    [InlineData("admin")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("TADMIN")]
    [InlineData("gestor")]
    public void Create_WhenValueIsInvalid_ShouldThrow(string value)
    {
        var act = () => Role.Create(value);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WhenValueIsNull_ShouldThrow()
    {
        var act = () => Role.Create(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_WhenSameValue_ShouldBeEqual()
    {
        var a = Role.Create("TAdmin");
        var b = Role.Create("TAdmin");
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_WhenDifferentValues_ShouldNotBeEqual()
    {
        var a = Role.Create("TAdmin");
        var b = Role.Create("Viewer");
        a.Should().NotBe(b);
    }

    [Fact]
    public void TAdmin_StaticProperty_ShouldBeValid()
    {
        Role.TAdmin.Value.Should().Be("TAdmin");
    }

    [Fact]
    public void GestorBU_StaticProperty_ShouldBeValid()
    {
        Role.GestorBU.Value.Should().Be("GestorBU");
    }

    [Fact]
    public void Vendedor_StaticProperty_ShouldBeValid()
    {
        Role.Vendedor.Value.Should().Be("Vendedor");
    }

    [Fact]
    public void Viewer_StaticProperty_ShouldBeValid()
    {
        Role.Viewer.Value.Should().Be("Viewer");
    }
}
