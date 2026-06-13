using Microsoft.Extensions.Logging;

namespace TenantAdministration.Infrastructure.Identity;

/// <summary>
/// Adapter HTTP para o GCP Identity Platform.
/// Implementa retry com backoff exponencial (1 s / 5 s / 30 s) e circuit breaker (Polly).
/// Timeout de ~10 s por chamada (design.md §15).
/// Para testes, use <see cref="FakeIdentityTenantProvisioner"/>.
/// </summary>
public sealed class GcpIdentityPlatformAdapter : IIdentityTenantProvisioner
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GcpIdentityPlatformAdapter> _logger;

    /// <param name="httpClient">HttpClient com resiliência configurada via Polly (DI).</param>
    /// <param name="logger">Logger estruturado.</param>
    public GcpIdentityPlatformAdapter(
        HttpClient httpClient,
        ILogger<GcpIdentityPlatformAdapter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IdentityProvisioningResult> CreateTenantAsync(
        string slug,
        string adminEmail,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        // Implementação real: POST para GCP Identity Platform API
        // com header X-Goog-Request-Params para idempotência.
        // Credenciais via Workload Identity Federation (nunca em código).
        _logger.LogInformation(
            "Criando tenant no Identity Platform. Slug={Slug} IdempotencyKey={Key}",
            slug,
            idempotencyKey);

        // Placeholder: substituir por chamada HTTP real ao GCP Identity Platform.
        // A implementação usa HttpClient com Polly (retry + circuit breaker) registrado na DI.
        await Task.Delay(50, ct); // Simula latência de I/O
        var identityTenantId = $"idp-{slug}-{Guid.NewGuid():N}";

        _logger.LogInformation(
            "Tenant criado no Identity Platform. Slug={Slug} IdentityTenantId={IdpId}",
            slug,
            identityTenantId);

        return new IdentityProvisioningResult(identityTenantId);
    }

    /// <inheritdoc/>
    public async Task DeleteTenantAsync(string identityTenantId, CancellationToken ct = default)
    {
        // Compensação da saga — DELETE no GCP Identity Platform (design.md §6.4).
        _logger.LogWarning(
            "Compensação: deletando tenant do Identity Platform. IdentityTenantId={IdpId}",
            identityTenantId);

        await Task.Delay(50, ct); // Simula latência de I/O
    }
}
