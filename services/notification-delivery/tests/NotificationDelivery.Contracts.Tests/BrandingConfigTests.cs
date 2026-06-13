using System.Reflection;
using FluentAssertions;
using NotificationDelivery.Contracts;
using Xunit;

namespace NotificationDelivery.Contracts.Tests;

/// <summary>
/// Testes do value object <see cref="BrandingConfig"/>.
/// Mapeia: Req 5, Req 5.2, DD-006, TASK-05/ST-01..ST-03.
///
/// Invariantes verificadas:
/// <list type="bullet">
///   <item><description>Cores hex inválidas rejeitadas na construção.</description></item>
///   <item><description><see cref="BrandingConfig.LogoUrl"/> vazio rejeitado.</description></item>
///   <item><description>Imutabilidade — sem setter público.</description></item>
///   <item><description>Architecture.Tests verde (sem tipo de provedor).</description></item>
/// </list>
/// </summary>
public sealed class BrandingConfigTests
{
    // -------------------------------------------------------------------------
    // Construção válida
    // -------------------------------------------------------------------------

    /// <summary>
    /// BrandingConfig com campos válidos deve construir sem erro e expor os valores.
    /// </summary>
    [Fact(DisplayName = "BrandingConfig válido deve construir sem erro e expor os campos")]
    public void BrandingConfig_ValidFields_ShouldConstruct()
    {
        // Arrange + Act
        var config = new BrandingConfig(
            logoUrl: "https://cdn.azim.com.br/logo.png",
            primaryColor: "#0F4C81",
            secondaryColor: "#FFFFFF");

        // Assert
        config.LogoUrl.Should().Be("https://cdn.azim.com.br/logo.png");
        config.PrimaryColor.Should().Be("#0F4C81");
        config.SecondaryColor.Should().Be("#FFFFFF");
    }

    // -------------------------------------------------------------------------
    // Imutabilidade (TASK-05/ST-01(d))
    // -------------------------------------------------------------------------

    /// <summary>
    /// BrandingConfig deve ser imutável após construção — sem setter público de instância.
    /// Mapeia: TASK-05/ST-01(d), design §4.3.
    /// </summary>
    [Fact(DisplayName = "BrandingConfig deve ser imutável após construção (design §4.3)")]
    public void BrandingConfig_ShouldBeImmutable_AfterConstruction()
    {
        // Act — verificar via reflexão que não há setters públicos de instância
        var publicSetters = typeof(BrandingConfig)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToArray();

        // Assert
        publicSetters.Should().BeEmpty(
            because: "BrandingConfig é um value object imutável — nenhuma propriedade deve ter setter público (design §4.3)");
    }

    // -------------------------------------------------------------------------
    // Validação de PrimaryColor inválida (TASK-05/ST-01(a)(b))
    // -------------------------------------------------------------------------

    /// <summary>
    /// PrimaryColor com nome de cor CSS inválido deve lançar ArgumentException.
    /// Mapeia: TASK-05/ST-01(a), Req 5.2.
    /// </summary>
    [Fact(DisplayName = "BrandingConfig com PrimaryColor='red' deve lançar ArgumentException (Req 5.2)")]
    public void BrandingConfig_WithColorName_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new BrandingConfig(
            logoUrl: "https://cdn.azim.com.br/logo.png",
            primaryColor: "red",
            secondaryColor: "#FFFFFF");

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("primaryColor");
    }

    /// <summary>
    /// PrimaryColor com valor hex truncado/inválido deve lançar ArgumentException.
    /// Mapeia: TASK-05/ST-01(b), Req 5.2.
    /// </summary>
    [Fact(DisplayName = "BrandingConfig com PrimaryColor='#GGG' deve lançar ArgumentException (Req 5.2)")]
    public void BrandingConfig_WithInvalidHex_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new BrandingConfig(
            logoUrl: "https://cdn.azim.com.br/logo.png",
            primaryColor: "#GGG",
            secondaryColor: "#FFFFFF");

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("primaryColor");
    }

    /// <summary>
    /// SecondaryColor com valor inválido deve lançar ArgumentException.
    /// </summary>
    [Fact(DisplayName = "BrandingConfig com SecondaryColor inválido deve lançar ArgumentException")]
    public void BrandingConfig_WithInvalidSecondaryColor_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new BrandingConfig(
            logoUrl: "https://cdn.azim.com.br/logo.png",
            primaryColor: "#0F4C81",
            secondaryColor: "invalid");

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("secondaryColor");
    }

    /// <summary>
    /// Cor hex com 3 dígitos (abreviada) deve ser rejeitada — o formato exigido é #RRGGBB.
    /// </summary>
    [Fact(DisplayName = "BrandingConfig com cor #RGB (3 dígitos) deve lançar ArgumentException")]
    public void BrandingConfig_WithShortHex_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new BrandingConfig(
            logoUrl: "https://cdn.azim.com.br/logo.png",
            primaryColor: "#ABC",
            secondaryColor: "#FFFFFF");

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("primaryColor");
    }

    // -------------------------------------------------------------------------
    // Validação de LogoUrl (TASK-05/ST-01(c))
    // -------------------------------------------------------------------------

    /// <summary>
    /// LogoUrl vazio deve lançar ArgumentException.
    /// Mapeia: TASK-05/ST-01(c).
    /// </summary>
    [Fact(DisplayName = "BrandingConfig com LogoUrl vazio deve lançar ArgumentException (TASK-05)")]
    public void BrandingConfig_WithEmptyLogoUrl_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new BrandingConfig(
            logoUrl: string.Empty,
            primaryColor: "#0F4C81",
            secondaryColor: "#FFFFFF");

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("logoUrl");
    }

    /// <summary>
    /// LogoUrl nulo deve lançar ArgumentException.
    /// </summary>
    [Fact(DisplayName = "BrandingConfig com LogoUrl nulo deve lançar ArgumentException")]
    public void BrandingConfig_WithNullLogoUrl_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new BrandingConfig(
            logoUrl: null!,
            primaryColor: "#0F4C81",
            secondaryColor: "#FFFFFF");

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("logoUrl");
    }

    // -------------------------------------------------------------------------
    // Igualdade por valor
    // -------------------------------------------------------------------------

    /// <summary>
    /// Dois BrandingConfig com os mesmos campos devem ser iguais por valor.
    /// </summary>
    [Fact(DisplayName = "BrandingConfig com mesmos campos deve ser igual por valor")]
    public void BrandingConfig_WithSameFields_ShouldBeEqualByValue()
    {
        // Arrange
        var a = new BrandingConfig("https://cdn.azim.com.br/logo.png", "#0F4C81", "#FFFFFF");
        var b = new BrandingConfig("https://cdn.azim.com.br/logo.png", "#0F4C81", "#FFFFFF");

        // Assert
        a.Should().Be(b);
    }

    // -------------------------------------------------------------------------
    // Aceita formatos hex válidos
    // -------------------------------------------------------------------------

    /// <summary>
    /// Cores hex em letras maiúsculas, minúsculas e mistas devem ser aceitas.
    /// </summary>
    [Theory(DisplayName = "BrandingConfig deve aceitar cor hex em maiúsculas e minúsculas")]
    [InlineData("#0f4c81", "#ffffff")]
    [InlineData("#0F4C81", "#FFFFFF")]
    [InlineData("#aAbBcC", "#123456")]
    public void BrandingConfig_WithValidHexVariants_ShouldConstruct(string primary, string secondary)
    {
        // Arrange + Act
        var act = () => new BrandingConfig("https://cdn.azim.com.br/logo.png", primary, secondary);

        // Assert
        act.Should().NotThrow();
    }
}
