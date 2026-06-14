using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AccountManagement.Domain.Tests.Accounts.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="Phone"/>.
///
/// Mapeia: design §4.3, TASK-02 ST-01, Req 5.2.
/// Invariante: apenas dígitos significativos retidos; opcional; igualdade por valor.
/// </summary>
public sealed class PhoneTests
{
    [Theory(DisplayName = "Cria Phone retendo apenas dígitos")]
    [InlineData("+55 (11) 98765-4321", "5511987654321")]
    [InlineData("11 98765-4321", "11987654321")]
    [InlineData("(21)3000-1234", "2130001234")]
    [InlineData("98765432", "98765432")]
    public void Create_RetainsOnlyDigits(string input, string expected)
    {
        var phone = Phone.Create(input);

        phone.Value.Should().Be(expected);
    }

    [Theory(DisplayName = "Lança ArgumentException para número sem dígitos")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("(+)")]
    public void Create_WithNoDigits_ThrowsArgumentException(string input)
    {
        var act = () => Phone.Create(input);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Lança ArgumentException para nulo")]
    public void Create_WithNull_ThrowsArgumentException()
    {
        var act = () => Phone.Create(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Igualdade por valor: dois Phone com mesmos dígitos são iguais")]
    public void Equality_SameDigits_AreEqual()
    {
        var a = Phone.Create("+55 (11) 98765-4321");
        var b = Phone.Create("5511987654321");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact(DisplayName = "Igualdade por valor: Phone com dígitos distintos não são iguais")]
    public void Equality_DifferentDigits_AreNotEqual()
    {
        var a = Phone.Create("11987654321");
        var b = Phone.Create("21987654321");

        a.Should().NotBe(b);
    }
}
