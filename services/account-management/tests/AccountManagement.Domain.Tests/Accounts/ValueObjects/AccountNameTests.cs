using AccountManagement.Domain.Accounts.Exceptions;
using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AccountManagement.Domain.Tests.Accounts.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="AccountName"/>.
///
/// Mapeia: design §4.3, TASK-02 ST-01, Req 1.5.
/// Invariante: nome não vazio após trim; comprimento máximo; igualdade por valor.
/// </summary>
public sealed class AccountNameTests
{
    [Fact(DisplayName = "Cria AccountName com nome válido")]
    public void Create_WithValidName_Succeeds()
    {
        var name = AccountName.Create("Pag.AI");

        name.Value.Should().Be("Pag.AI");
    }

    [Theory(DisplayName = "Lança AccountNameRequiredException para nome vazio ou somente espaços")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithEmptyOrWhitespace_ThrowsAccountNameRequiredException(string input)
    {
        var act = () => AccountName.Create(input);

        act.Should().Throw<AccountNameRequiredException>();
    }

    [Fact(DisplayName = "Lança AccountNameRequiredException para nome nulo")]
    public void Create_WithNull_ThrowsAccountNameRequiredException()
    {
        var act = () => AccountName.Create(null!);

        act.Should().Throw<AccountNameRequiredException>();
    }

    [Fact(DisplayName = "Lança AccountNameRequiredException para nome que excede comprimento máximo")]
    public void Create_WithNameExceedingMaxLength_ThrowsAccountNameRequiredException()
    {
        var tooLong = new string('a', AccountName.MaxLength + 1);

        var act = () => AccountName.Create(tooLong);

        act.Should().Throw<AccountNameRequiredException>();
    }

    [Fact(DisplayName = "Igualdade por valor: dois AccountName com mesmo valor são iguais")]
    public void Equality_SameValue_AreEqual()
    {
        var a = AccountName.Create("Pag.AI");
        var b = AccountName.Create("Pag.AI");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact(DisplayName = "Igualdade por valor: AccountName com valores distintos não são iguais")]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = AccountName.Create("Pag.AI");
        var b = AccountName.Create("Outra Empresa");

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact(DisplayName = "ToString retorna o valor do nome")]
    public void ToString_ReturnsValue()
    {
        var name = AccountName.Create("Pag.AI");

        name.ToString().Should().Be("Pag.AI");
    }
}
