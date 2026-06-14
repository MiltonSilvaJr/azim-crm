using FluentAssertions;
using PartnerManagement.Domain.Partners.Exceptions;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Domain.Tests.Partners.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="PartnerRole"/>.
/// Mapeia: Req 5, design §4.3, DD-005, TASK-03.
/// Nota: a validação contra a lista canônica do tenant ocorre no agregado (via ICanonicalRoleProvider),
/// não no VO. O VO apenas encapsula o valor não-vazio.
/// </summary>
public sealed class PartnerRoleTests
{
    [Fact(DisplayName = "PartnerRole — string vazia lança InvalidPartnerRoleException")]
    public void Create_EmptyString_ThrowsInvalidPartnerRoleException()
    {
        Action act = () => PartnerRole.Create(string.Empty);
        act.Should().Throw<InvalidPartnerRoleException>();
    }

    [Fact(DisplayName = "PartnerRole — string nula lança InvalidPartnerRoleException")]
    public void Create_Null_ThrowsInvalidPartnerRoleException()
    {
        Action act = () => PartnerRole.Create(null!);
        act.Should().Throw<InvalidPartnerRoleException>();
    }

    [Fact(DisplayName = "PartnerRole — string somente espaços lança InvalidPartnerRoleException")]
    public void Create_WhitespaceOnly_ThrowsInvalidPartnerRoleException()
    {
        Action act = () => PartnerRole.Create("   ");
        act.Should().Throw<InvalidPartnerRoleException>();
    }

    [Theory(DisplayName = "PartnerRole — papéis do seed canônico são aceitos como valores válidos")]
    [InlineData("Indicador")]
    [InlineData("Revendedor")]
    [InlineData("Distribuidor")]
    [InlineData("Integrador")]
    public void Create_CanonicalSeedRole_Succeeds(string role)
    {
        Action act = () => PartnerRole.Create(role);
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "PartnerRole — valor preservado sem transformação")]
    public void Create_ValidRole_PreservesValue()
    {
        PartnerRole role = PartnerRole.Create("Distribuidor");
        role.Value.Should().Be("Distribuidor");
    }

    [Fact(DisplayName = "PartnerRole — igualdade por valor: mesmos papéis são iguais")]
    public void EqualityByValue_SameRoles_AreEqual()
    {
        PartnerRole a = PartnerRole.Create("Indicador");
        PartnerRole b = PartnerRole.Create("Indicador");
        a.Should().Be(b);
    }

    [Fact(DisplayName = "PartnerRole — igualdade por valor: papéis diferentes não são iguais")]
    public void EqualityByValue_DifferentRoles_AreNotEqual()
    {
        PartnerRole a = PartnerRole.Create("Indicador");
        PartnerRole b = PartnerRole.Create("Revendedor");
        a.Should().NotBe(b);
    }
}
