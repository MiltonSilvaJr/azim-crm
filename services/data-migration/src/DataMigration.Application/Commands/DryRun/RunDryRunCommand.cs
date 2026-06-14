using MediatR;

namespace DataMigration.Application.Commands.DryRun;

/// <summary>
/// Resultado do dry-run.
/// </summary>
/// <param name="JobId">ID do job atualizado.</param>
/// <param name="Status">Estado resultante: sempre "dry_run_completed".</param>
/// <param name="Report">Relatório de triagem gerado.</param>
public sealed record RunDryRunResult(
    Guid JobId,
    string Status,
    DTOs.TriageReportDto Report);

/// <summary>
/// Command de execução do dry-run.
///
/// Executa simulação completa do import em transação rollback-only.
/// Gera <c>TriageReport</c>; não persiste conteúdo de domínio (Req 2.1).
/// Transita o job para <c>dry_run_completed</c>.
///
/// Rastreia: design §5.3, Req 2, PBT-04, TASK-09.
/// </summary>
/// <param name="JobId">ID do <c>MigrationJob</c> em estado <c>Created</c>.</param>
/// <param name="FileStream">Stream do arquivo .xlsx para leitura das linhas.</param>
public sealed record RunDryRunCommand(
    Guid JobId,
    Stream FileStream) : IRequest<RunDryRunResult>;
