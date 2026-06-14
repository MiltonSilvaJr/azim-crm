using FluentAssertions;
using PartnerManagement.Domain.Partners.Exceptions;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Domain.Tests.Partners.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="Email"/>.
/// Mapeia: Req 7.2, RNF 4, design §4.3, TASK-04.
/// </summary>
public sealed class EmailTests
{
    [Theory(DisplayName = "Email — formato inválido lança InvalidPartnerContactException")]
    [InlineData("nao-e-email")]
    [InlineData("@semdominio.com")]
    [InlineData("sem-arroba.com")]
    [InlineData("duplo@@arroba.com")]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_InvalidFormat_ThrowsInvalidPartnerContactException(string value)
    {
        Action act = () => Email.Create(value);
        act.Should().Throw<InvalidPartnerContactException>();
    }

    [Fact(DisplayName = "Email — nulo lança InvalidPartnerContactException")]
    public void Create_Null_ThrowsInvalidPartnerContactException()
    {
        Action act = () => Email.Create(null!);
        act.Should().Throw<InvalidPartnerContactException>();
    }

    [Theory(DisplayName = "Email — formato válido é aceito")]
    [InlineData("usuario@exemplo.com")]
    [InlineData("contato@empresa.com.br")]
    [InlineData("a@b.co")]
    public void Create_ValidFormat_Succeeds(string value)
    {
        Action act = () => Email.Create(value);
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "Email — normalizado para minúsculas")]
    public void Create_UpperCase_NormalizesToLowerCase()
    {
        Email email = Email.Create("Usuario@Exemplo.COM");
        email.Value.Should().Be("usuario@exemplo.com");
    }

    [Fact(DisplayName = "Email — igualdade por valor: mesmos e-mails são iguais")]
    public void EqualityByValue_SameEmails_AreEqual()
    {
        Email a = Email.Create("contato@empresa.com");
        Email b = Email.Create("CONTATO@empresa.com");
        a.Should().Be(b);
    }

    [Fact(DisplayName = "Email — igualdade por valor: e-mails diferentes não são iguais")]
    public void EqualityByValue_DifferentEmails_AreNotEqual()
    {
        Email a = Email.Create("a@empresa.com");
        Email b = Email.Create("b@empresa.com");
        a.Should().NotBe(b);
    }
}
