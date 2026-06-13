using TenantAdministration.Application.Ports;

namespace TenantAdministration.Infrastructure.Storage;

/// <summary>
/// Implementação fake de <see cref="IBrandingAssetStorage"/> para testes.
/// Armazena uploads em memória e retorna URLs CDN determinísticas.
/// </summary>
public sealed class FakeBrandingAssetStorage : IBrandingAssetStorage
{
    private readonly List<UploadRecord> _uploads = [];

    /// <summary>Registros de uploads realizados (leitura para testes).</summary>
    public IReadOnlyList<UploadRecord> Uploads => _uploads.AsReadOnly();

    /// <summary>Quando verdadeiro, <see cref="UploadAsync"/> lança exceção.</summary>
    public bool ShouldFail { get; set; }

    /// <inheritdoc/>
    public Task<string> UploadAsync(
        Guid tenantId,
        string slug,
        string type,
        Stream content,
        CancellationToken ct = default)
    {
        if (ShouldFail)
            throw new InvalidOperationException("Falha simulada no upload do GCS.");

        var cdnUrl = $"https://cdn.fake.azim.com.br/tenants/{slug}/{type}";
        _uploads.Add(new UploadRecord(tenantId, slug, type, cdnUrl, DateTimeOffset.UtcNow));
        return Task.FromResult(cdnUrl);
    }

    /// <summary>Reseta o estado do fake.</summary>
    public void Reset()
    {
        _uploads.Clear();
        ShouldFail = false;
    }
}

/// <summary>Registro de upload realizado pelo <see cref="FakeBrandingAssetStorage"/>.</summary>
/// <param name="TenantId">Identificador do tenant.</param>
/// <param name="Slug">Slug do tenant.</param>
/// <param name="Type">Tipo do asset (logo ou favicon).</param>
/// <param name="CdnUrl">URL CDN retornada.</param>
/// <param name="UploadedAt">Instante do upload.</param>
public sealed record UploadRecord(
    Guid TenantId,
    string Slug,
    string Type,
    string CdnUrl,
    DateTimeOffset UploadedAt);
