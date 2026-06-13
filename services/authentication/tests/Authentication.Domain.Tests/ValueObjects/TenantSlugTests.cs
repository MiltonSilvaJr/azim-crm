using Authentication.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Authentication.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários de <see cref="TenantSlug"/>.
///
/// Mapeia: Req 1 (resolução de tenant pelo slug), design.md § 4.3.
/// Critérios: imutabilidade, igualdade por valor, invariantes de formato.
/// </summary>
public sealed class TenantSlugTests
{
    // =========================================================================
    // Invariantes de criação
    // =========================================================================

    [Fact(DisplayName = "TenantSlug deve lançar ArgumentException para valor nulo")]
    public void Create_WithNull_ShouldThrow()
    {
        var act = () => TenantSlug.Create(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "TenantSlug deve lançar ArgumentException para valor vazio ou whitespace")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithEmptyOrWhitespace_ShouldThrow(string value)
    {
        var act = () => TenantSlug.Create(value);
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "TenantSlug deve normalizar para lowercase e trim")]
    public void Create_WithUpperCaseAndSpaces_ShouldNormalize()
    {
        var slug = TenantSlug.Create("  AZIM-Demo  ");
        slug.Value.Should().Be("azim-demo");
    }

    [Fact(DisplayName = "TenantSlug válido deve preservar valor normalizado")]
    public void Create_WithValidValue_ShouldPreserveNormalizedValue()
    {
        var slug = TenantSlug.Create("azim-crm");
        slug.Value.Should().Be("azim-crm");
    }

    // =========================================================================
    // Igualdade por valor
    // =========================================================================

    [Fact(DisplayName = "Dois TenantSlug com mesmo valor devem ser iguais")]
    public void Equality_SameValue_ShouldBeEqual()
    {
        var a = TenantSlug.Create("azim-crm");
        var b = TenantSlug.Create("azim-crm");
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact(DisplayName = "Dois TenantSlug com valores diferentes não devem ser iguais")]
    public void Equality_DifferentValues_ShouldNotBeEqual()
    {
        var a = TenantSlug.Create("tenant-a");
        var b = TenantSlug.Create("tenant-b");
        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact(DisplayName = "TenantSlug normalizado deve ser igual ao original com maiúsculas")]
    public void Equality_NormalizedVsOriginal_ShouldBeEqual()
    {
        var lower = TenantSlug.Create("azim-crm");
        var upper = TenantSlug.Create("AZIM-CRM");
        lower.Should().Be(upper);
    }

    // =========================================================================
    // Imutabilidade
    // =========================================================================

    [Fact(DisplayName = "TenantSlug não deve expor setter público no Value")]
    public void Immutability_ValueProperty_ShouldHaveNoPublicSetter()
    {
        var prop = typeof(TenantSlug).GetProperty(nameof(TenantSlug.Value));
        prop.Should().NotBeNull();
        prop!.SetMethod?.IsPublic.Should().BeFalse("Value deve ser somente leitura");
    }
}
