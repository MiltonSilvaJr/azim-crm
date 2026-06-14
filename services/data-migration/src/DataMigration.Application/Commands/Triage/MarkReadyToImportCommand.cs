using System.Text.Json;
using DataMigration.Application.DTOs;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using DataMigration.Domain.Aggregates;
using DataMigration.Domain.Policies;
using MediatR;

namespace DataMigration.Application.Commands.Triage;

/// <summary>Resultado do mark ready.</summary>
/// <param name="JobId">ID do job.</param>
/// <param name="Status">Estado resultante: sempre "ready_to_import".</param>
public sealed record MarkReadyToImportResult(Guid JobId, string Status);

/// <summary>
/// Command de marcação de job como pronto para import.
///
/// Valida pendências obrigatórias (owners) via <see cref="OwnerRequiredSpecification"/>.
/// Rejeita com MIG-ERR-006 enquanto existir oportunidade sem owner.
/// Parceiros sem percentual e estágios faltantes NÃO bloqueam.
///
/// Rastreia: design §5.1, Req 4.5, MIG-ERR-006, PBT-07, TASK-10.
/// </summary>
public sealed record MarkReadyToImportCommand(Guid JobId) : IRequest<MarkReadyToImportResult>;

/// <summary>
/// Handler de marcação de job como pronto para import.
///
/// Rastreia: design §5.1, Req 4.5, MIG-ERR-006, PBT-07, TASK-10.
/// </summary>
public sealed class MarkReadyToImportHandler
    : IRequestHandler<MarkReadyToImportCommand, MarkReadyToImportResult>
{
    private readonly IMigrationJobRepository _repository;
    private readonly IClock _clock;

    /// <summary>Cria o handler com as dependências injetadas.</summary>
    public MarkReadyToImportHandler(IMigrationJobRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<MarkReadyToImportResult> Handle(
        MarkReadyToImportCommand request,
        CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new MigrationDomainException("MIG-ERR-004", $"Job não encontrado: {request.JobId}.");

        // Verifica owners obrigatórios (OwnerRequiredSpecification, PBT-07)
        var resolution = AssignOwnerHandler.LoadOrNew(job.TriageResolutionJson);

        // O TriageReport contém o total de oportunidades detectadas no dry-run
        var totalOpportunities = GetTotalOpportunities(job.TriageReportJson);
        var assignedOwners = resolution.OwnerAssignments.Count;
        var ownerlessCount = Math.Max(0, totalOpportunities - assignedOwners);

        if (!OwnerRequiredSpecification.IsSatisfied(ownerlessCount))
        {
            throw new MigrationDomainException(
                "MIG-ERR-006",
                $"Import bloqueado: há {ownerlessCount} oportunidade(s) sem responsável. " +
                $"Atribua owner a todas as oportunidades antes de marcar como pronto.");
        }

        // Transita para ReadyToImport
        job.TransitionTo(MigrationJobStatus.ReadyToImport, _clock.UtcNow);
        await _repository.UpdateAsync(job, cancellationToken);

        return new MarkReadyToImportResult(job.Id, "ready_to_import");
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
            // Se o JSON estiver malformado, não bloqueia o import apenas por isso
        }

        return 0;
    }
}
