using MediatR;

namespace DataMigration.Application.Commands.Upload;

/// <summary>
/// Resultado do upload de planilha.
/// </summary>
/// <param name="JobId">ID do <c>MigrationJob</c> criado.</param>
/// <param name="Status">Estado inicial: sempre "created".</param>
/// <param name="DetectedRowCount">Número de linhas detectadas.</param>
public sealed record UploadSpreadsheetResult(
    Guid JobId,
    string Status,
    int DetectedRowCount);

/// <summary>
/// Command de upload de planilha .xlsx.
///
/// Valida extensão/MIME, tamanho e estrutura de colunas; cria <c>MigrationJob</c>
/// em estado <c>Created</c> com metadados do arquivo.
/// Não persiste conteúdo de domínio (Req 1.4).
///
/// Erros: MIG-ERR-001 (não-.xlsx), MIG-ERR-002 (colunas ausentes), MIG-ERR-003 (tamanho).
///
/// Rastreia: design §5.1, §5.3, Req 1, TASK-08.
/// </summary>
/// <param name="FileName">Nome original do arquivo (ex: "Pipeline Vellus.xlsx").</param>
/// <param name="FileStream">Stream do arquivo para parsing de estrutura.</param>
/// <param name="FileSizeBytes">Tamanho do arquivo em bytes.</param>
/// <param name="FileHash">Hash SHA-256 do arquivo para auditoria (Req 13).</param>
public sealed record UploadSpreadsheetCommand(
    string FileName,
    Stream FileStream,
    long FileSizeBytes,
    string FileHash) : IRequest<UploadSpreadsheetResult>;
