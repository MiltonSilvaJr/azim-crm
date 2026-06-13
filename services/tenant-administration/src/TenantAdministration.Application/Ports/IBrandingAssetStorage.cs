namespace TenantAdministration.Application.Ports;

/// <summary>
/// Porta de saída para armazenamento de assets de branding (logo e favicon).
/// Implementação concreta vem na Onda 4 (Infrastructure — GCS).
/// design.md §6.2 e §6.4.
/// </summary>
public interface IBrandingAssetStorage
{
    /// <summary>
    /// Faz upload de um asset de branding e retorna a URL pública via CDN.
    /// O path no bucket segue o padrão <c>tenants/{slug}/{type}</c>.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant (para organização no bucket).</param>
    /// <param name="slug">Slug do tenant (para path no bucket).</param>
    /// <param name="type">Tipo do asset: <c>logo</c> ou <c>favicon</c>.</param>
    /// <param name="content">Stream do conteúdo do arquivo.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>URL CDN do asset armazenado.</returns>
    Task<string> UploadAsync(
        Guid tenantId,
        string slug,
        string type,
        Stream content,
        CancellationToken ct = default);
}
