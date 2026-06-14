using FluentAssertions;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Domain.Tests.Partners.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="Phone"/>.
/// Mapeia: Req 7.1, design §4.3, TASK-04.
/// </summary>
public sealed class PhoneTests
{
    [Theory(DisplayName = "Phone — aceita dígitos significativos")]
    [InlineData("11999998888")]
    [InlineData("1134567890")]
    [InlineData("5511999998888")]
    public void Create_DigitsOnly_Succeeds(string value)
    {
        Action act = () => Phone.Create(value);
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "Phone — preserva valor de dígitos")]
    public void Create_Digits_PreservesValue()
    {
        Phone phone = Phone.Create("11999998888");
        phone.Value.Should().Be("11999998888");
    }

    [Theory(DisplayName = "Phone — valor vazio ou somente espaços lança ArgumentException")]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhitespace_ThrowsArgumentException(string value)
    {
        Action act = () => Phone.Create(value);
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Phone — nulo lança ArgumentException")]
    public void Create_Null_ThrowsArgumentException()
    {
        Action act = () => Phone.Create(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Phone — igualdade por valor: mesmos telefones são iguais")]
    public void EqualityByValue_SamePhones_AreEqual()
    {
        Phone a = Phone.Create("11999998888");
        Phone b = Phone.Create("11999998888");
        a.Should().Be(b);
    }

    [Fact(DisplayName = "Phone — igualdade por valor: telefones diferentes não são iguais")]
    public void EqualityByValue_DifferentPhones_AreNotEqual()
    {
        Phone a = Phone.Create("11999998888");
        Phone b = Phone.Create("11999997777");
        a.Should().NotBe(b);
    }
}
