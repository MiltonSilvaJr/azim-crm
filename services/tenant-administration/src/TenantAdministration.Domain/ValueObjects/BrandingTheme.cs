namespace TenantAdministration.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que agrega exatamente os quatro elementos do white-label estrito:
/// <c>logoUrl</c>, <c>faviconUrl</c> e <c>ColorPair colors</c>.
/// PBT-03: o conjunto de campos persistidos é subconjunto de {logo, favicon, cor primária, cor secundária}.
/// Qualquer campo fora do conjunto é rejeitado pelo tipo (não existe propriedade para CSS/fonte/layout).
/// </summary>
public sealed record BrandingTheme(
    string? LogoUrl,
    string? FaviconUrl,
    ColorPair Colors)
{
    /// <summary>
    /// Cria um <see cref="BrandingTheme"/> com os elementos do white-label estrito.
    /// As URLs podem ser nulas antes do primeiro upload de assets.
    /// </summary>
    /// <param name="logoUrl">URL do logo (GCS/CDN). Pode ser nulo.</param>
    /// <param name="faviconUrl">URL do favicon (GCS/CDN). Pode ser nulo.</param>
    /// <param name="colors">Par de cores primária/secundária validado.</param>
    public static BrandingTheme Create(string? logoUrl, string? faviconUrl, ColorPair colors) =>
        new(logoUrl, faviconUrl, colors);
}
