using FluentAssertions;
using PartnerManagement.Domain.Partners.Exceptions;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Domain.Tests.Partners.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="PartnerContact"/>.
/// Mapeia: Req 7, RNF 4, design §4.3, DD-008, TASK-04.
/// </summary>
public sealed class PartnerContactTests
{
    [Fact(DisplayName = "PartnerContact — e-mail inválido lança InvalidPartnerContactException")]
    public void Create_InvalidEmail_ThrowsInvalidPartnerContactException()
    {
        Action act = () => PartnerContact.Create("nao-e-email", null);
        act.Should().Throw<InvalidPartnerContactException>();
    }

    [Fact(DisplayName = "PartnerContact — e-mail válido e sem telefone é aceito")]
    public void Create_ValidEmailNoPhone_Succeeds()
    {
        Action act = () => PartnerContact.Create("contato@empresa.com", null);
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "PartnerContact — e-mail e telefone válidos são aceitos")]
    public void Create_ValidEmailAndPhone_Succeeds()
    {
        Action act = () => PartnerContact.Create("contato@empresa.com", "11999998888");
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "PartnerContact.ToMasked — não retorna e-mail em claro")]
    public void ToMasked_DoesNotExposeEmailInClear()
    {
        PartnerContact contact = PartnerContact.Create("contato@empresa.com", "11999998888");
        string masked = contact.ToMasked();
        masked.Should().NotContain("contato@empresa.com");
        masked.Should().NotContain("empresa.com");
    }

    [Fact(DisplayName = "PartnerContact.ToMasked — não retorna telefone em claro")]
    public void ToMasked_DoesNotExposePhoneInClear()
    {
        PartnerContact contact = PartnerContact.Create("contato@empresa.com", "11999998888");
        string masked = contact.ToMasked();
        masked.Should().NotContain("11999998888");
    }

    [Fact(DisplayName = "PartnerContact.ToMasked — funciona sem telefone")]
    public void ToMasked_WithoutPhone_DoesNotThrow()
    {
        PartnerContact contact = PartnerContact.Create("contato@empresa.com", null);
        Action act = () => contact.ToMasked();
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "PartnerContact — igualdade por valor: mesmos contatos são iguais")]
    public void EqualityByValue_SameContacts_AreEqual()
    {
        PartnerContact a = PartnerContact.Create("a@b.com", "11999998888");
        PartnerContact b = PartnerContact.Create("a@b.com", "11999998888");
        a.Should().Be(b);
    }

    [Fact(DisplayName = "PartnerContact — igualdade por valor: contatos com e-mails diferentes não são iguais")]
    public void EqualityByValue_DifferentEmails_AreNotEqual()
    {
        PartnerContact a = PartnerContact.Create("a@b.com", null);
        PartnerContact b = PartnerContact.Create("c@d.com", null);
        a.Should().NotBe(b);
    }

    [Fact(DisplayName = "PartnerContact — ToString não expõe PII (DD-008)")]
    public void ToString_DoesNotExposePii()
    {
        PartnerContact contact = PartnerContact.Create("sensivel@empresa.com", "11999998888");
        string str = contact.ToString();
        str.Should().NotContain("sensivel@empresa.com");
        str.Should().NotContain("11999998888");
    }
}
