using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AccountManagement.Domain.Tests.Accounts.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="NormalizedName"/>.
///
/// Mapeia: design §4.3, TASK-02 ST-01, Req 1.1, Req 3.2, PBT-01, PBT-02.
/// Invariante: resultado determinístico de <see cref="Services.NameNormalizer"/>;
/// imutável; igualdade por valor; base de dedupe e busca.
/// </summary>
public sealed class NormalizedNameTests
{
    [Fact(DisplayName = "Cria NormalizedName com valor normalizado")]
    public void Create_WithNormalizedValue_Succeeds()
    {
        var name = NormalizedName.Create("pagiai");

        name.Value.Should().Be("pagiai");
    }

    [Fact(DisplayName = "Igualdade por valor: dois NormalizedName com mesmo valor são iguais")]
    public void Equality_SameValue_AreEqual()
    {
        var a = NormalizedName.Create("pagiai");
        var b = NormalizedName.Create("pagiai");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact(DisplayName = "Igualdade por valor: NormalizedName com valores distintos não são iguais")]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = NormalizedName.Create("pagiai");
        var b = NormalizedName.Create("outra empresa");

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact(DisplayName = "ToString retorna o valor normalizado")]
    public void ToString_ReturnsValue()
    {
        var name = NormalizedName.Create("pagiai");

        name.ToString().Should().Be("pagiai");
    }
}
