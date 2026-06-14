namespace Reporting.Contracts.Responses;

/// <summary>
/// Resposta do export CSV (Req 5).
/// Contém URL assinada de curta validade (~15 min) para download do artefato no Cloud Storage.
/// Mapeia: TASK-11, design §5.2, DD-004.
/// </summary>
public sealed record CsvExportResponse(
    string ReportType,
    string Filename,
    string SignedUrl,
    DateTimeOffset ExpiresAt);
