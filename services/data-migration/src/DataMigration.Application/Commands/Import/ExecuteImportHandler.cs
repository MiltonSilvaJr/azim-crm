using System.Text.Json;
using DataMigration.Application.Commands.Triage;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using DataMigration.Domain.Aggregates;
using DataMigration.Domain.Policies;
using MediatR;

namespace DataMigration.Application.Commands.Import;

/// <summary>
/// Handler do import transacional tudo-ou-nada.
///
/// Núcleo de atomicidade (RN-023, design §5.3):
///   1. Verifica <c>confirmation = true</c> (MIG-ERR-008).
///   2. Recupera job; valida estado <c>ReadyToImport</c> (MIG-ERR-005).
///   3. Aplica <c>OwnerRequiredSpecification</c> (MIG-ERR-006).
///   4. Abre transação única (<c>IUnitOfWork.BeginAsync</c>).
///   5. Executa pipeline via <c>ImportTransactionService</c>.
///   6. Sucesso: COMMIT + transição <c>Completed</c> + <c>ImportReport</c>.
///   7. Falha: ROLLBACK + transição <c>RolledBack</c> + MIG-ERR-007.
///
/// Rastreia: design §5.3, Req 6, Req 12, DD-001, PBT-01, PBT-02, TASK-11.
/// </summary>
public sealed class ExecuteImportHandler
    : IRequestHandler<ExecuteImportCommand, ExecuteImportResult>
{
    private readonly IMigrationJobRepository _repository;
    private readonly IUnitOfWork _uow;
    private readonly ISpreadsheetParser _parser;
    private readonly ImportTransactionService _importService;
    private readonly IClock _clock;

    /// <summary>Cria o handler com as dependências injetadas.</summary>
    public ExecuteImportHandler(
        IMigrationJobRepository repository,
        IUnitOfWork uow,
        ISpreadsheetParser parser,
        IAccountImportPort accountPort,
        IPartnerImportPort partnerPort,
        IOpportunityImportPort opportunityPort,
        IActivityImportPort activityPort,
        IOpportunityNumberPort numberPort,
        IOrganizationReadPort orgPort,
        IClock clock)
    {
        _repository = repository;
        _uow = uow;
        _parser = parser;
        _clock = clock;
        _importService = new ImportTransactionService(
            accountPort, partnerPort, opportunityPort,
            activityPort, numberPort, orgPort, clock);
    }

    /// <inheritdoc />
    public async Task<ExecuteImportResult> Handle(
        ExecuteImportCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Confirmação explícita (Req 6.2, MIG-ERR-008)
        if (!request.Confirmation)
        {
            throw new MigrationDomainException(
                "MIG-ERR-008",
                "Confirmação explícita obrigatória para executar o import. " +
                "Reenvie com confirmation: true.");
        }

        // 2. Recupera e valida o job
        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new MigrationDomainException("MIG-ERR-004", $"Job não encontrado: {request.JobId}.");

        if (job.Status != MigrationJobStatus.ReadyToImport)
        {
            throw new MigrationDomainException(
                "MIG-ERR-005",
                $"Transição de estado inválida: job está em '{job.Status}'; " +
                $"esperado 'ReadyToImport'.");
        }

        // 3. Valida OwnerRequiredSpecification (PBT-07)
        var resolution = AssignOwnerHandler.LoadOrNew(job.TriageResolutionJson);
        var totalOpportunities = GetTotalOpportunities(job.TriageReportJson);
        var ownerlessCount = Math.Max(0, totalOpportunities - resolution.OwnerAssignments.Count);

        if (!OwnerRequiredSpecification.IsSatisfied(ownerlessCount))
        {
            throw new MigrationDomainException(
                "MIG-ERR-006",
                $"Import bloqueado: há {ownerlessCount} oportunidade(s) sem responsável.");
        }

        // 4. Lê as linhas da planilha
        var rows = await _parser.ParseRowsAsync(request.FileStream, cancellationToken);

        // 5. Transita para Importing
        job.TransitionToImporting(ownerlessCandidateCount: 0, at: _clock.UtcNow);

        // 6. Abre transação única (tudo-ou-nada — RN-023, DD-001)
        await _uow.BeginAsync(cancellationToken);

        try
        {
            // 7. Executa pipeline de import em ordem determinística
            var report = await _importService.ExecuteAsync(job, rows, resolution, cancellationToken);

            // 8. Sucesso: COMMIT + Completed + ImportReport
            await _uow.CommitAsync(cancellationToken);

            var reportJson = JsonSerializer.Serialize(report);
            job.SetImportReport(reportJson, _clock.UtcNow);
            job.TransitionTo(MigrationJobStatus.Completed, _clock.UtcNow);
            await _repository.UpdateAsync(job, cancellationToken);

            return new ExecuteImportResult(
                JobId: job.Id,
                Status: "completed",
                Report: report);
        }
        catch (Exception ex) when (ex is not MigrationDomainException { ErrorCode: "MIG-ERR-008" or "MIG-ERR-004" or "MIG-ERR-005" or "MIG-ERR-006" })
        {
            // 9. Falha: ROLLBACK total (tudo-ou-nada — PBT-01)
            await _uow.RollbackAsync(cancellationToken);

            job.TransitionTo(MigrationJobStatus.RolledBack, _clock.UtcNow);
            await _repository.UpdateAsync(job, cancellationToken);

            throw new MigrationDomainException(
                "MIG-ERR-007",
                "Import revertido; nenhum dado persistido (MSG-034).",
                ex);
        }
    }

    private static int GetTotalOpportunities(string? triageReportJson)
    {
        if (string.IsNullOrWhiteSpace(triageReportJson))
        {
            return 0;
        }

        try
        {
            using var doc = JsonDocument.Parse(triageReportJson);
            if (doc.RootElement.TryGetProperty("totalOpportunities", out var prop))
            {
                return prop.GetInt32();
            }
        }
        catch (JsonException)
        {
            // JSON malformado: não bloqueia
        }

        return 0;
    }
}
