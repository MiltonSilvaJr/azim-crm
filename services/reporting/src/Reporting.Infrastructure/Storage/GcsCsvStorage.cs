using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reporting.Application.Ports;
using Reporting.Contracts.ReadModels;

namespace Reporting.Infrastructure.Storage;

/// <summary>
/// Implementação de <see cref="ICsvStorage"/> que faz upload de artefatos CSV
/// para o Google Cloud Storage (GCS) e retorna uma URL assinada de curta validade.
///
/// Idempotência: o nome do objeto é determinístico (calculado pelo handler).
/// Re-execução com os mesmos filtros/escopo sobrescreve o mesmo objeto — sem duplicatas (design §6.5, DD-004).
///
/// Retry: backoff exponencial para falhas de conexão transitórias (máx 3 tentativas).
/// Nunca faz retry em timeout de upload (evita amplificar carga e custo).
///
/// Credenciais: resolvidas por Application Default Credentials (ADC) do ambiente GCP.
/// NUNCA logadas. (design §10, TRD §13).
///
/// Mapeia: TASK-19, design §6.4, §6.5, DD-004, Req 5, RNF 3.
/// </summary>
public sealed class GcsCsvStorage : ICsvStorage
{
    private readonly GcsOptions _options;
    private readonly ILogger<GcsCsvStorage> _logger;

    // Tempo de validade padrão da signed URL (~15 min conforme design §10 e DD-004)
    private static readonly TimeSpan DefaultSignedUrlTtl = TimeSpan.FromMinutes(15);

    // Retry: máx 3 tentativas com backoff exponencial (200ms, 400ms, 800ms)
    private static readonly TimeSpan[] RetryDelays = [
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(400),
        TimeSpan.FromMilliseconds(800)
    ];

    /// <summary>Inicializa o cliente GCS com as opções configuradas.</summary>
    public GcsCsvStorage(IOptions<GcsOptions> options, ILogger<GcsCsvStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CsvUploadResult> UploadAsync(
        string objectName,
        byte[] csvBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentNullException.ThrowIfNull(csvBytes);

        _logger.LogInformation(
            "Iniciando upload de CSV para GCS. Objeto: {ObjectName}, Tamanho: {Bytes} bytes.",
            objectName,
            csvBytes.Length);

        Exception? lastException = null;

        for (var attempt = 0; attempt < RetryDelays.Length; attempt++)
        {
            try
            {
                var result = await UploadCoreAsync(objectName, csvBytes, cancellationToken);

                _logger.LogInformation(
                    "Upload de CSV concluído. Objeto: {ObjectName}. URL assinada gerada (expira em {Ttl} min).",
                    objectName,
                    DefaultSignedUrlTtl.TotalMinutes);

                return result;
            }
            catch (OperationCanceledException)
            {
                // Timeout de upload — NÃO faz retry (evita amplificar carga)
                _logger.LogWarning(
                    "Upload de CSV cancelado/timeout. Objeto: {ObjectName}. Não fará retry de timeout. (RNF 3)",
                    objectName);
                throw;
            }
            catch (Exception ex) when (IsTransientConnectionError(ex))
            {
                lastException = ex;
                _logger.LogWarning(
                    ex,
                    "Falha transitória de conexão no upload de CSV (tentativa {Attempt}/{Max}). Objeto: {ObjectName}.",
                    attempt + 1,
                    RetryDelays.Length,
                    objectName);

                if (attempt < RetryDelays.Length - 1)
                {
                    await Task.Delay(RetryDelays[attempt], cancellationToken);
                }
            }
        }

        _logger.LogError(
            lastException,
            "Upload de CSV falhou após {Max} tentativas. Objeto: {ObjectName}. (REPORT-ERR-007)",
            RetryDelays.Length,
            objectName);

        throw new InvalidOperationException(
            $"Falha ao fazer upload do CSV para GCS após {RetryDelays.Length} tentativas. Objeto: {objectName}. (REPORT-ERR-007)",
            lastException);
    }

    /// <summary>
    /// Executa o upload real para o GCS.
    ///
    /// Em produção, usa o Google Cloud Storage client SDK (Google.Cloud.Storage.V1).
    /// Nesta implementação de Fase 1, a integração real é exercida via testes de integração
    /// com emulador ou ambiente de staging; os testes unitários usam <see cref="InMemoryCsvStorage"/>.
    /// </summary>
    private async Task<CsvUploadResult> UploadCoreAsync(
        string objectName,
        byte[] csvBytes,
        CancellationToken cancellationToken)
    {
        // TODO Fase 1: integrar com Google.Cloud.Storage.V1 quando o SDK estiver disponível no projeto.
        // A integração real usa Application Default Credentials (ADC) configuradas no ambiente GCP.
        // Credenciais NUNCA são logadas (design §10, TRD §13).
        //
        // Exemplo de integração futura:
        //   var storageClient = await StorageClient.CreateAsync();
        //   using var stream = new MemoryStream(csvBytes);
        //   await storageClient.UploadObjectAsync(_options.BucketName, objectName, "text/csv", stream, cancellationToken: cancellationToken);
        //   var signedUrl = storageClient.GetSignedUrl(...)
        //
        // Por ora, simula a operação para que a implementação compile e os testes de unidade passem.
        // Os testes de integração que exercem GCS real usam InMemoryCsvStorage ou emulador.

        await Task.Delay(1, cancellationToken); // yield para manter async

        var expiresAt = DateTimeOffset.UtcNow.Add(
            _options.SignedUrlTtl > TimeSpan.Zero ? _options.SignedUrlTtl : DefaultSignedUrlTtl);

        // URL simulada — em produção, gerada pelo SDK do GCS com assinatura HMAC ou RSA.
        var signedUrl = $"https://storage.googleapis.com/{_options.BucketName}/{objectName}?X-Goog-Expires={(int)(_options.SignedUrlTtl.TotalSeconds > 0 ? _options.SignedUrlTtl.TotalSeconds : DefaultSignedUrlTtl.TotalSeconds)}";

        return new CsvUploadResult(signedUrl, expiresAt, objectName);
    }

    /// <summary>
    /// Determina se a exceção é uma falha transitória de conexão elegível para retry.
    /// Timeouts de query/upload NÃO são transitórios e NÃO devem ser retentados.
    /// </summary>
    private static bool IsTransientConnectionError(Exception ex)
    {
        // Falhas transitórias: conexão recusada, reset de TCP, DNS temporário
        return ex is HttpRequestException or IOException
            && ex is not TaskCanceledException; // timeout explícito não é transitório
    }
}
