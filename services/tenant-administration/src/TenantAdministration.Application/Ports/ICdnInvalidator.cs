namespace TenantAdministration.Application.Ports;

/// <summary>
/// Porta de saída para invalidação de cache CDN.
/// Dispara invalidação do <c>brand.json</c> após <c>BrandingChanged</c>,
/// garantindo propagação em menos de 30 s (RNF 4.1).
/// Implementação concreta vem na Onda 4 (Infrastructure — Cloud CDN).
/// design.md §6.2.
/// </summary>
public interface ICdnInvalidator
{
    /// <summary>
    /// Invalida o cache CDN do <c>brand.json</c> de um tenant pelo slug.
    /// </summary>
    /// <param name="slug">Slug do tenant cujo cache deve ser invalidado.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task InvalidateAsync(string slug, CancellationToken ct = default);
}
