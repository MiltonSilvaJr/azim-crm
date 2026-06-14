using System.Text.Json;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using DataMigration.Domain.Aggregates;
using MediatR;

namespace DataMigration.Application.Commands.DryRun;

/// <summary>
/// Handler do dry-run.
///
/// Executa simulação completa do import em transação marcada para rollback
/// incondicional (DD-001). Nenhum dado de domínio persiste.
/// Gera <c>TriageReport</c> e persiste no <c>MigrationJob</c>.
/// Transita o job para <c>DryRunCompleted</c>.
///
/// Rastreia: design §5.3, Req 2, PBT-04, DD-001, TASK-09.
/// </summary>
public sealed class RunDryRunHandler
    : IRequestHandler<RunDryRunCommand, RunDryRunResult>
{
    private readonly ISpreadsheetParser _parser;
    private readonly IMigrationJobRepository _repository;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    /// <summary>
    /// Cria o handler com as dependências injetadas.
    /// </summary>
    public RunDryRunHandler(
        ISpreadsheetParser parser,
        IMigrationJobRepository repository,
        IUnitOfWork uow,
        IClock clock)
    {
        _parser = parser;
        _repository = repository;
        _uow = uow;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<RunDryRunResult> Handle(
        RunDryRunCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Recupera o job
        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken);
        if (job is null)
        {
            throw new MigrationDomainException(
                "MIG-ERR-004",
                $"Job de migração não encontrado: {request.JobId}.");
        }

        // 2. Abre transação rollback-only (simulação sem efeitos colaterais — Req 2.1, DD-001)
        await _uow.BeginRollbackOnlyAsync(cancellationToken);

        try
        {
            // 3. Lê todas as linhas das abas
            var rows = await _parser.ParseRowsAsync(request.FileStream, cancellationToken);

            // 4. Simula o pipeline (CanonicalRowMapper → policies) via DryRunSimulator
            var report = DryRunSimulator.Simulate(rows);

            // 5. Persiste o TriageReport no job (JSONB — DD-007)
            var reportJson = JsonSerializer.Serialize(report);
            job.SetTriageReport(reportJson, _clock.UtcNow);

            // 6. Transita para DryRunCompleted
            job.TransitionTo(MigrationJobStatus.DryRunCompleted, _clock.UtcNow);

            // 7. Salva as atualizações do job (fora da transação rollback-only)
            await _repository.UpdateAsync(job, cancellationToken);

            return new RunDryRunResult(
                JobId: job.Id,
                Status: "dry_run_completed",
                Report: report);
        }
        finally
        {
            // 8. Rollback incondicional — nenhuma escrita de domínio persiste
            await _uow.RollbackAsync(cancellationToken);
        }
    }
}
