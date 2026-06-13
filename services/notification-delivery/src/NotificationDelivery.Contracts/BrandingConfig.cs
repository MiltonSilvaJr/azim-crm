using System.Text.RegularExpressions;

namespace NotificationDelivery.Contracts;

/// <summary>
/// Configuração de branding do tenant aplicada ao template de e-mail.
///
/// Value object imutável com igualdade por valor.
/// Restringe-se ao branding estrito DEC-004 — sem CSS arbitrário por tenant (Req 5.2).
///
/// Invariantes (design §4.3, Req 5, DD-006):
/// <list type="bullet">
///   <item><description><see cref="LogoUrl"/> não pode ser nulo nem vazio.</description></item>
///   <item><description><see cref="PrimaryColor"/> e <see cref="SecondaryColor"/> devem estar no formato <c>#RRGGBB</c>.</description></item>
/// </list>
///
/// Decisão: ausência de branding é representada por <c>null</c> na <see cref="EmailMessage"/>,
/// tratada pelo <c>BrandingEmailDecorator</c> com o tema padrão (design §5.5, DD-006).
/// </summary>
public sealed class BrandingConfig : IEquatable<BrandingConfig>
{
    // -------------------------------------------------------------------------
    // Constante de validação
    // -------------------------------------------------------------------------

    /// <summary>
    /// Regex que valida cores no formato <c>#RRGGBB</c> (seis dígitos hex, case-insensitive).
    /// Mapeia: Req 5.2, DD-006. Rejeitamos #RGB, nomes CSS e qualquer outro formato.
    /// </summary>
    private static readonly Regex HexColorRegex =
        new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    // -------------------------------------------------------------------------
    // Propriedades imutáveis
    // -------------------------------------------------------------------------

    /// <summary>URL pública do logotipo do tenant.</summary>
    public string LogoUrl { get; }

    /// <summary>Cor primária em formato hex <c>#RRGGBB</c>.</summary>
    public string PrimaryColor { get; }

    /// <summary>Cor secundária em formato hex <c>#RRGGBB</c>.</summary>
    public string SecondaryColor { get; }

    // -------------------------------------------------------------------------
    // Construtor com validação
    // -------------------------------------------------------------------------

    /// <summary>
    /// Constrói um <see cref="BrandingConfig"/> validando todos os campos.
    /// </summary>
    /// <param name="logoUrl">URL pública do logotipo do tenant. Não pode ser nulo nem vazio.</param>
    /// <param name="primaryColor">Cor primária em formato hex <c>#RRGGBB</c>.</param>
    /// <param name="secondaryColor">Cor secundária em formato hex <c>#RRGGBB</c>.</param>
    /// <exception cref="ArgumentException">Quando qualquer campo for inválido.</exception>
    public BrandingConfig(string logoUrl, string primaryColor, string secondaryColor)
    {
        LogoUrl = ValidateLogoUrl(logoUrl);
        PrimaryColor = ValidateHexColor(primaryColor, nameof(primaryColor));
        SecondaryColor = ValidateHexColor(secondaryColor, nameof(secondaryColor));
    }

    // -------------------------------------------------------------------------
    // Igualdade por valor
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public bool Equals(BrandingConfig? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return LogoUrl == other.LogoUrl
            && PrimaryColor == other.PrimaryColor
            && SecondaryColor == other.SecondaryColor;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as BrandingConfig);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(LogoUrl, PrimaryColor, SecondaryColor);

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(BrandingConfig? left, BrandingConfig? right) =>
        Equals(left, right);

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(BrandingConfig? left, BrandingConfig? right) =>
        !Equals(left, right);

    // -------------------------------------------------------------------------
    // Validações de invariante
    // -------------------------------------------------------------------------

    private static string ValidateLogoUrl(string logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            throw new ArgumentException(
                "A URL do logotipo não pode ser nula, vazia ou apenas espaços em branco.",
                nameof(logoUrl));

        return logoUrl;
    }

    private static string ValidateHexColor(string color, string paramName)
    {
        if (string.IsNullOrWhiteSpace(color) || !HexColorRegex.IsMatch(color))
            throw new ArgumentException(
                "A cor deve estar no formato hex '#RRGGBB' (seis dígitos hexadecimais). Valor inválido recebido.",
                paramName);

        return color;
    }
}
