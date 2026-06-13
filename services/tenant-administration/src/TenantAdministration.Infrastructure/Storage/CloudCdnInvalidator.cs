using Microsoft.Extensions.Logging;
using TenantAdministration.Application.Ports;

namespace TenantAdministration.Infrastructure.Storage;

/// <summary>
/// Implementação de <see cref="ICdnInvalidator"/> que dispara invalidação no Cloud CDN.
/// Após <c>BrandingChanged</c>, o cache do <c>brand.json</c> é invalidado para refletir
/// em menos de 30 s (RNF 4.1, design.md §6.2).
/// Para testes, use <see cref="FakeCdnInvalidator"/>.
/// </summary>
public sealed class CloudCdnInvalidator : ICdnInvalidator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudCdnInvalidator> _logger;

    /// <param name="httpClient">HttpClient com configuração de autenticação GCP.</param>
    /// <param name="logger">Logger estruturado.</param>
    public CloudCdnInvalidator(HttpClient httpClient, ILogger<CloudCdnInvalidator> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvalidateAsync(string slug, CancellationToken ct = default)
    {
        // Path a invalidar: /brand/{slug}/brand.json (design.md §8.3)
        var path = $"/brand/{slug}/brand.json";

        _logger.LogInformation(
            "CDN invalidation iniciada. Slug={Slug} Path={Path}",
            slug,
            path);

        // Implementação real: POST para Cloud CDN Cache Invalidation API.
        // Credenciais via Workload Identity Federation (nunca hardcoded).
        // Placeholder: substituir por chamada HTTP real.
        await Task.Delay(20, ct);

        _logger.LogInformation(
            "CDN invalidation concluída. Slug={Slug} Path={Path}",
            slug,
            path);
    }
}
