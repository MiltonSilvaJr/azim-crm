using FluentAssertions;
using PartnerManagement.Domain.Partners.Exceptions;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Domain.Tests.Partners.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="PartnerName"/>.
/// Mapeia: Req 1.1, RNF 4, design §4.3, TASK-03.
/// </summary>
public sealed class PartnerNameTests
{
    [Fact(DisplayName = "PartnerName — string vazia lança PartnerNameRequiredException")]
    public void Create_EmptyString_ThrowsPartnerNameRequiredException()
    {
        Action act = () => PartnerName.Create(string.Empty);
        act.Should().Throw<PartnerNameRequiredException>();
    }

    [Fact(DisplayName = "PartnerName — string somente espaços lança PartnerNameRequiredException")]
    public void Create_WhitespaceOnly_ThrowsPartnerNameRequiredException()
    {
        Action act = () => PartnerName.Create("   ");
        act.Should().Throw<PartnerNameRequiredException>();
    }

    [Fact(DisplayName = "PartnerName — valor nulo lança PartnerNameRequiredException")]
    public void Create_Null_ThrowsPartnerNameRequiredException()
    {
        Action act = () => PartnerName.Create(null!);
        act.Should().Throw<PartnerNameRequiredException>();
    }

    [Fact(DisplayName = "PartnerName — nome com espaços nas bordas é trimado e preservado")]
    public void Create_WithLeadingTrailingSpaces_PreservesTrimmedValue()
    {
        PartnerName name = PartnerName.Create("  Acme Corp  ");
        name.Value.Should().Be("Acme Corp");
    }

    [Fact(DisplayName = "PartnerName — nome válido preserva exatamente o valor trimado")]
    public void Create_ValidName_PreservesValue()
    {
        PartnerName name = PartnerName.Create("Distribuidor ABC");
        name.Value.Should().Be("Distribuidor ABC");
    }

    [Fact(DisplayName = "PartnerName — igualdade por valor: mesmos nomes são iguais")]
    public void EqualityByValue_SameNames_AreEqual()
    {
        PartnerName a = PartnerName.Create("Acme");
        PartnerName b = PartnerName.Create("Acme");
        a.Should().Be(b);
    }

    [Fact(DisplayName = "PartnerName — igualdade por valor: nomes diferentes não são iguais")]
    public void EqualityByValue_DifferentNames_AreNotEqual()
    {
        PartnerName a = PartnerName.Create("Acme");
        PartnerName b = PartnerName.Create("Beta");
        a.Should().NotBe(b);
    }

    [Fact(DisplayName = "PartnerName — ToString não expõe PII (retorna valor mascarado)")]
    public void ToString_DoesNotExposePii()
    {
        // Conforme DD-008: name é tratado como possível PII.
        // ToString não deve retornar o valor em claro.
        PartnerName name = PartnerName.Create("Informação Sensível");
        string str = name.ToString();
        str.Should().NotContain("Informação Sensível");
    }
}
