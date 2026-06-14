using MediatR;

namespace DataMigration.Application.Commands.Import;

/// <summary>
/// Resultado do import transacional.
/// </summary>
/// <param name="JobId">ID do job.</param>
/// <param name="Status">Estado resultante: "completed" ou "rolled_back".</param>
/// <param name="Report">Relatório final do import (disponível apenas em "completed").</param>
public sealed record ExecuteImportResult(
    Guid JobId,
    string Status,
    DTOs.ImportReportDto? Report = null);

/// <summary>
/// Command de execução do import transacional tudo-ou-nada.
///
/// Requer <c>Confirmation = true</c> (Req 6.2, MIG-ERR-008).
/// Job deve estar em <c>ReadyToImport</c> (MIG-ERR-005).
/// Qualquer falha → ROLLBACK total + <c>rolled_back</c> + MIG-ERR-007.
///
/// Rastreia: design §5.1, §5.3, Req 6, Req 12, DD-001, PBT-01, PBT-02, TASK-11.
/// </summary>
/// <param name="JobId">ID do job em estado <c>ReadyToImport</c>.</param>
/// <param name="Confirmation">Deve ser <c>true</c>; protege contra import acidental.</param>
/// <param name="FileStream">Stream do arquivo .xlsx com as linhas a importar.</param>
public sealed record ExecuteImportCommand(
    Guid JobId,
    bool Confirmation,
    Stream FileStream) : IRequest<ExecuteImportResult>;
