using AccountManagement.Domain.Accounts.ValueObjects;
using AccountManagement.Domain.Accounts.Exceptions;
using FluentAssertions;
using Xunit;

namespace AccountManagement.Domain.Tests.Accounts.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="ContactInfo"/>.
///
/// Mapeia: design §4.3, TASK-02 ST-01, Req 5.2, RNF 1.
/// Invariante: nome obrigatório; e-mail/telefone opcionais;
/// <see cref="ContactInfo.ToMasked"/> não expõe nome, e-mail nem telefone em claro (RNF 1).
/// </summary>
public sealed class ContactInfoTests
{
    [Fact(DisplayName = "Cria ContactInfo com nome válido")]
    public void Create_WithValidName_Succeeds()
    {
        var info = ContactInfo.Create("João Silva");

        info.Name.Should().Be("João Silva");
        info.Email.Should().BeNull();
        info.Phone.Should().BeNull();
    }

    [Fact(DisplayName = "Cria ContactInfo com nome, e-mail e telefone")]
    public void Create_WithAllFields_Succeeds()
    {
        var info = ContactInfo.Create(
            "Maria Souza",
            Email.Create("maria@azim.com"),
            Phone.Create("11987654321"));

        info.Name.Should().Be("Maria Souza");
        info.Email!.Value.Should().Be("maria@azim.com");
        info.Phone!.Value.Should().Be("11987654321");
    }

    [Theory(DisplayName = "Lança AccountNameRequiredException quando nome do contato é vazio")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithEmptyName_ThrowsAccountNameRequiredException(string name)
    {
        var act = () => ContactInfo.Create(name);

        act.Should().Throw<AccountNameRequiredException>();
    }

    [Fact(DisplayName = "Lança AccountNameRequiredException quando nome do contato é nulo")]
    public void Create_WithNullName_ThrowsAccountNameRequiredException()
    {
        var act = () => ContactInfo.Create(null!);

        act.Should().Throw<AccountNameRequiredException>();
    }

    [Fact(DisplayName = "ToMasked não expõe nome em texto claro")]
    public void ToMasked_DoesNotExposeNameInClear()
    {
        var info = ContactInfo.Create("João Silva");

        var masked = info.ToMasked();

        masked.Name.Should().NotBe("João Silva");
        masked.Name.Should().Be(ContactInfo.AnonymizationMarker);
    }

    [Fact(DisplayName = "ToMasked não expõe e-mail em texto claro")]
    public void ToMasked_DoesNotExposeEmailInClear()
    {
        var info = ContactInfo.Create("Ana Lima", Email.Create("ana@azim.com"));

        var masked = info.ToMasked();

        masked.Email.Should().BeNull();
    }

    [Fact(DisplayName = "ToMasked não expõe telefone em texto claro")]
    public void ToMasked_DoesNotExposePhoneInClear()
    {
        var info = ContactInfo.Create("Carlos", phone: Phone.Create("11987654321"));

        var masked = info.ToMasked();

        masked.Phone.Should().BeNull();
    }

    [Fact(DisplayName = "ToMasked retorna marcador de anonimização consistente")]
    public void ToMasked_UsesAnonymizationMarker()
    {
        var info = ContactInfo.Create("Fernanda");

        var masked = info.ToMasked();

        masked.Name.Should().Be(ContactInfo.AnonymizationMarker);
    }

    [Fact(DisplayName = "Igualdade por valor: dois ContactInfo com mesmos dados são iguais")]
    public void Equality_SameData_AreEqual()
    {
        var a = ContactInfo.Create("João Silva", Email.Create("joao@azim.com"));
        var b = ContactInfo.Create("João Silva", Email.Create("joao@azim.com"));

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact(DisplayName = "Igualdade por valor: ContactInfo com nomes distintos não são iguais")]
    public void Equality_DifferentNames_AreNotEqual()
    {
        var a = ContactInfo.Create("João");
        var b = ContactInfo.Create("Maria");

        a.Should().NotBe(b);
    }
}
