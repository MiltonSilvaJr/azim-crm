using Reporting.Application.Ports;
using Reporting.Contracts.ReadModels;

namespace Reporting.Infrastructure.Storage;

/// <summary>
/// Implementação in-memory de <see cref="ICsvStorage"/> para uso em testes de integração
/// e ambientes de desenvolvimento sem acesso ao GCS real.
///
/// Comportamento idempotente: mesmo <paramref name="objectName"/> sobrescreve o conteúdo anterior.
/// Signed URL simulada com validade configurável.
///
/// Mapeia: TASK-19, design §6.5, DD-004 (idempotência de upload).
/// </summary>
public sealed class InMemoryCsvStorage : ICsvStorage
{
    private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);
    private readonly TimeSpan _signedUrlTtl;

    /// <summary>Inicializa o storage in-memory com o TTL da URL assinada.</summary>
    /// <param name="signedUrlTtl">Validade da URL assinada. Padrão: 15 minutos.</param>
    public InMemoryCsvStorage(TimeSpan signedUrlTtl = default)
    {
        _signedUrlTtl = signedUrlTtl > TimeSpan.Zero ? signedUrlTtl : TimeSpan.FromMinutes(15);
    }

    /// <inheritdoc/>
    public Task<CsvUploadResult> UploadAsync(
        string objectName,
        byte[] csvBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentNullException.ThrowIfNull(csvBytes);

        // Idempotente: sobrescreve o objeto se já existir (design §6.5, DD-004)
        _store[objectName] = csvBytes;

        var expiresAt = DateTimeOffset.UtcNow.Add(_signedUrlTtl);
        var signedUrl = $"https://storage.test/bucket/{objectName}?expires={expiresAt:O}";

        return Task.FromResult(new CsvUploadResult(signedUrl, expiresAt, objectName));
    }

    /// <summary>Retorna o conteúdo do objeto armazenado (para asserções em testes).</summary>
    public byte[]? GetStored(string objectName)
    {
        _store.TryGetValue(objectName, out var bytes);
        return bytes;
    }

    /// <summary>Verifica se o objeto existe no storage in-memory.</summary>
    public bool Contains(string objectName) => _store.ContainsKey(objectName);

    /// <summary>Número de objetos armazenados.</summary>
    public int Count => _store.Count;
}
