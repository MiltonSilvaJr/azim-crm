using System.Text.RegularExpressions;
using TenantAdministration.Domain.Common;
using TenantAdministration.Domain.Errors;

namespace TenantAdministration.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que representa o par de cores primária e secundária do branding.
/// Formato: <c>#RRGGBB</c> normalizado para maiúsculas.
/// </summary>
public sealed class ColorPair : IEquatable<ColorPair>
{
    private static readonly Regex HexPattern =
        new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    /// <summary>Cor primária em <c>#RRGGBB</c> maiúsculo.</summary>
    public string Primary { get; }

    /// <summary>Cor secundária em <c>#RRGGBB</c> maiúsculo.</summary>
    public string Secondary { get; }

    private ColorPair(string primary, string secondary)
    {
        Primary = primary;
        Secondary = secondary;
    }

    /// <summary>
    /// Cria um <see cref="ColorPair"/> validando e normalizando ambas as cores para maiúsculas.
    /// </summary>
    /// <param name="primary">Cor primária (ex.: <c>#1A73E8</c> ou <c>#1a73e8</c>).</param>
    /// <param name="secondary">Cor secundária.</param>
    /// <returns>Sucesso com o par ou falha com TA-ERR-004.</returns>
    public static Result<ColorPair> Create(string primary, string secondary)
    {
        if (!IsValidHex(primary) || !IsValidHex(secondary))
        {
            var (code, message) = DomainErrors.ColorInvalidFormat;
            return Result<ColorPair>.Failure(code, message);
        }

        return Result<ColorPair>.Success(new ColorPair(
            primary.ToUpperInvariant(),
            secondary.ToUpperInvariant()));
    }

    private static bool IsValidHex(string? color) =>
        !string.IsNullOrEmpty(color) && HexPattern.IsMatch(color);

    /// <summary>
    /// Calcula a luminância relativa WCAG 2.1 de uma cor hex.
    /// Fórmula sRGB linearizada conforme WCAG 2.1 §1.4.3.
    /// </summary>
    internal static double RelativeLuminance(string hexColor)
    {
        var r = HexToLinear(hexColor[1..3]);
        var g = HexToLinear(hexColor[3..5]);
        var b = HexToLinear(hexColor[5..7]);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double HexToLinear(string hex)
    {
        var value = Convert.ToInt32(hex, 16) / 255.0;
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    /// <inheritdoc/>
    public bool Equals(ColorPair? other) =>
        other is not null && Primary == other.Primary && Secondary == other.Secondary;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ColorPair other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Primary, Secondary);

    /// <inheritdoc/>
    public override string ToString() => $"({Primary}, {Secondary})";

    /// <summary>Igualdade estrutural por valor.</summary>
    public static bool operator ==(ColorPair? left, ColorPair? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Desigualdade estrutural por valor.</summary>
    public static bool operator !=(ColorPair? left, ColorPair? right) => !(left == right);
}
