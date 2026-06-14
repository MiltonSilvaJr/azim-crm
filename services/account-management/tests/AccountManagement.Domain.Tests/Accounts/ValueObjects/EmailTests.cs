using AccountManagement.Domain.Accounts.Exceptions;
using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AccountManagement.Domain.Tests.Accounts.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="Email"/>.
///
/// Mapeia: design §4.3, TASK-02 ST-01, Req 5.4.
/// Invariante: formato válido (regex RFC simplificada); normalização para minúsculas;
/// igualdade por valor; lança <see cref="InvalidEmailException"/> se inválido.
/// </summary>
public sealed class EmailTests
{
    [Theory(DisplayName = "Cria Email com endereço válido")]
    [InlineData("usuario@exemplo.com")]
    [InlineData("USUARIO@EXEMPLO.COM")]
    [InlineData("user.name+tag@sub.domain.org")]
    public void Create_WithValidEmail_Succeeds(string input)
    {
        var email = Email.Create(input);

        email.Value.Should().Be(input.ToLowerInvariant());
    }

    [Fact(DisplayName = "Email é normalizado para minúsculas")]
    public void Create_NormalizesToLowercase()
    {
        var email = Email.Create("ADMIN@AZIM.COM.BR");

        email.Value.Should().Be("admin@azim.com.br");
    }

    [Theory(DisplayName = "Lança InvalidEmailException para formato inválido")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nao-e-email")]
    [InlineData("@semdominio")]
    [InlineData("semarroba.com")]
    [InlineData("duplo@@arroba.com")]
    public void Create_WithInvalidFormat_ThrowsInvalidEmailException(string input)
    {
        var act = () => Email.Create(input);

        act.Should().Throw<InvalidEmailException>();
    }

    [Fact(DisplayName = "Lança InvalidEmailException para nulo")]
    public void Create_WithNull_ThrowsInvalidEmailException()
    {
        var act = () => Email.Create(null!);

        act.Should().Throw<InvalidEmailException>();
    }

    [Fact(DisplayName = "Igualdade por valor: dois Email com mesmo endereço são iguais")]
    public void Equality_SameValue_AreEqual()
    {
        var a = Email.Create("user@azim.com");
        var b = Email.Create("USER@AZIM.COM");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact(DisplayName = "Igualdade por valor: Email com endereços distintos não são iguais")]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = Email.Create("a@azim.com");
        var b = Email.Create("b@azim.com");

        a.Should().NotBe(b);
    }
}
