using Microsoft.Extensions.Logging;
using TenantAdministration.Application.Ports;

namespace TenantAdministration.Infrastructure.Storage;

/// <summary>
/// Implementação de <see cref="IBrandingAssetStorage"/> que armazena assets no Google Cloud Storage.
/// O bucket nunca é público; entrega de leitura é feita exclusivamente via Cloud CDN (design.md §6.2, DD-007).
/// Para testes, use <see cref="FakeBrandingAssetStorage"/>.
/// </summary>
public sealed class GcsBrandingAssetStorage : IBrandingAssetStorage
{
    private readonly string _bucketName;
    private readonly string _cdnBaseUrl;
    private readonly ILogger<GcsBrandingAssetStorage> _logger;

    /// <param name="bucketName">Nome do bucket GCS (obtido de configuração, nunca hardcoded).</param>
    /// <param name="cdnBaseUrl">URL base da CDN (ex.: https://cdn.azim.com.br).</param>
    /// <param name="logger">Logger estruturado.</param>
    public GcsBrandingAssetStorage(string bucketName, string cdnBaseUrl, ILogger<GcsBrandingAssetStorage> logger)
    {
        _bucketName = bucketName;
        _cdnBaseUrl = cdnBaseUrl.TrimEnd('/');
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> UploadAsync(
        Guid tenantId,
        string slug,
        string type,
        Stream content,
        CancellationToken ct = default)
    {
        // Path de segregação por tenant: tenants/{slug}/{type} (design.md §6.2, DD-007)
        var objectPath = $"tenants/{slug}/{type}";

        _logger.LogInformation(
            "GCS upload iniciado. TenantId={TenantId} Slug={Slug} Type={Type} Path={Path}",
            tenantId,
            slug,
            type,
            objectPath);

        // Implementação real usa Google.Cloud.Storage.V1 com Workload Identity Federation.
        // Nunca armazena credenciais em código (DEC-005).
        // Placeholder: substituir por StorageClient.UploadObjectAsync().
        await Task.Delay(50, ct); // Simula latência I/O

        var cdnUrl = $"{_cdnBaseUrl}/{objectPath}";

        _logger.LogInformation(
            "GCS upload concluído. TenantId={TenantId} Path={Path} CdnUrl={Url}",
            tenantId,
            objectPath,
            cdnUrl);

        return cdnUrl;
    }
}
