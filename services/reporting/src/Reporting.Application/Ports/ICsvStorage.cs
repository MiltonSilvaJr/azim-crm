using Reporting.Contracts.ReadModels;

namespace Reporting.Application.Ports;

/// <summary>
/// Porta de armazenamento de artefatos CSV no Cloud Storage (GCS).
///
/// Implementada na Infrastructure (Google Cloud Storage client).
/// O nome do objeto é determinístico (calculado pelo handler) para idempotência (design §6.5, DD-004).
///
/// Mapeia: TASK-05, design §5.2, §6.4, DD-004, Req 5.
/// </summary>
public interface ICsvStorage
{
    /// <summary>
    /// Faz upload dos bytes CSV para o bucket configurado e retorna a URL assinada de curta validade.
    /// </summary>
    /// <param name="objectName">
    ///   Nome determinístico do objeto no bucket.
    ///   Formato: <c>reports/{tenant_id}/{report_type}/{period_hash}/{scope_hash}.csv</c>
    /// </param>
    /// <param name="csvBytes">Conteúdo do arquivo CSV (UTF-8 com BOM).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns><see cref="CsvUploadResult"/> com <see cref="CsvUploadResult.SignedUrl"/> e <see cref="CsvUploadResult.ExpiresAt"/>.</returns>
    Task<CsvUploadResult> UploadAsync(
        string objectName,
        byte[] csvBytes,
        CancellationToken cancellationToken = default);
}
