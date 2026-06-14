namespace Reporting.Contracts.ReadModels;

/// <summary>
/// Resultado do upload de CSV para o Cloud Storage.
/// Retornado por <c>ICsvStorage.UploadAsync</c>.
/// Mapeia: TASK-05, design §6.4, DD-004, Req 5.
/// </summary>
public sealed record CsvUploadResult(
    string SignedUrl,
    DateTimeOffset ExpiresAt,
    string ObjectName);
