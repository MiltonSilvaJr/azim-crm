using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using TenantAdministration.Domain.ValueObjects;
using Xunit;

namespace TenantAdministration.Domain.Tests.ValueObjects;

/// <summary>
/// Testes para BrandingTheme.
/// Cobre TASK-04: PBT-03 — white-label estrito.
/// </summary>
public sealed class BrandingThemeTests
{
    private static ColorPair DefaultColors() =>
        ColorPair.Create("#1A73E8", "#34A853").Value;

    [Fact]
    public void Create_WithAllFields_ReturnsSuccess()
    {
        var theme = BrandingTheme.Create("https://cdn/logo.svg", "https://cdn/fav.png", DefaultColors());
        theme.Should().NotBeNull();
        theme.LogoUrl.Should().Be("https://cdn/logo.svg");
        theme.FaviconUrl.Should().Be("https://cdn/fav.png");
        theme.Colors.Should().Be(DefaultColors());
    }

    [Fact]
    public void Create_WithNullUrls_IsAllowed()
    {
        // Logo/favicon podem ser nulos antes do primeiro upload
        var theme = BrandingTheme.Create(null, null, DefaultColors());
        theme.Should().NotBeNull();
        theme.LogoUrl.Should().BeNull();
        theme.FaviconUrl.Should().BeNull();
    }

    // ──────────────────────────────────────────────
    // PBT-03 — white-label estrito: exatamente {logo, favicon, colors}
    // ──────────────────────────────────────────────

    /// <summary>
    /// PBT-03: O tipo BrandingTheme possui exatamente as propriedades {LogoUrl, FaviconUrl, Colors}.
    /// Nenhuma propriedade de CSS, fonte, layout ou qualquer outro campo existe no tipo.
    /// A ausência de propriedade é a garantia de white-label estrito por construção.
    /// </summary>
    [Fact(DisplayName = "PBT-03: Conjunto de propriedades = {LogoUrl, FaviconUrl, Colors}")]
    public void Pbt03_BrandingThemeHasExactlyWhiteLabelFields()
    {
        var type = typeof(BrandingTheme);
        var publicProperties = type.GetProperties()
            .Where(p => p.CanRead)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToArray();

        var expected = new[] { "Colors", "FaviconUrl", "LogoUrl" };

        publicProperties.Should().BeEquivalentTo(expected,
            because: "BrandingTheme deve ter EXATAMENTE {LogoUrl, FaviconUrl, Colors} — white-label estrito (DD-004, PBT-03)");
    }

    [Fact]
    public void BrandingTheme_IsImmutable()
    {
        var type = typeof(BrandingTheme);
        foreach (var prop in type.GetProperties())
        {
            var setter = prop.SetMethod;
            if (setter is null) continue;
            (setter.IsPrivate || setter.ReturnParameter
                .GetRequiredCustomModifiers()
                .Any(m => m.FullName?.Contains("IsExternalInit") == true))
                .Should().BeTrue(because: $"{prop.Name} não deve ter setter público");
        }
    }

    [Fact]
    public void BrandingTheme_EqualityByValue()
    {
        var colors = DefaultColors();
        var a = BrandingTheme.Create("https://cdn/logo.svg", "https://cdn/fav.png", colors);
        var b = BrandingTheme.Create("https://cdn/logo.svg", "https://cdn/fav.png", colors);
        a.Should().Be(b);
    }
}
