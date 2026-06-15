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

    [Fact(DisplayName = "PartnerName — ToString retorna representação neutra (design defensivo de VO)")]
    public void ToString_ReturnsNeutralRepresentation()
    {
        // ToString retorna representação neutra por design defensivo do objeto de valor,
        // evitando vazamento acidental em interpolações de string e logs não estruturados.
        // Nota: name não é PII por VAL-PARTNER-01 (2026-06-15); o comportamento defensivo
        // do ToString é mantido por boa prática de VO, não por obrigação de mascaramento.
        // Use PartnerName.Value em contextos que precisem do valor em claro.
        PartnerName name = PartnerName.Create("Informação Qualquer");
        string str = name.ToString();
        str.Should().Be("[PartnerName]");
    }
}
