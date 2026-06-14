using System.Text.Json;
using DataMigration.Application.DTOs;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using MediatR;

namespace DataMigration.Application.Commands.Triage;

/// <summary>Resultado da atribuição em massa.</summary>
/// <param name="AssignedCount">Número de oportunidades atualizadas.</param>
public sealed record BulkAssignOwnerResult(int AssignedCount);

/// <summary>
/// Command de atribuição em massa de owner a todas as oportunidades de uma BU.
///
/// Rastreia: design §5.1, Req 4.2, TASK-10.
/// </summary>
public sealed record BulkAssignOwnerCommand(
    Guid JobId,
    string BuName,
    Guid OwnerId,
    IReadOnlyList<int> RowIndexes) : IRequest<BulkAssignOwnerResult>;

/// <summary>
/// Handler de atribuição em massa por BU.
///
/// Rastreia: design §5.1, Req 4.2, DD-007, TASK-10.
/// </summary>
public sealed class BulkAssignOwnerHandler
    : IRequestHandler<BulkAssignOwnerCommand, BulkAssignOwnerResult>
{
    private readonly IMigrationJobRepository _repository;
    private readonly IClock _clock;

    /// <summary>Cria o handler com as dependências injetadas.</summary>
    public BulkAssignOwnerHandler(IMigrationJobRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<BulkAssignOwnerResult> Handle(
        BulkAssignOwnerCommand request,
        CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new MigrationDomainException("MIG-ERR-004", $"Job não encontrado: {request.JobId}.");

        var resolution = AssignOwnerHandler.LoadOrNew(job.TriageResolutionJson);
        var assignments = resolution.OwnerAssignments.ToList();

        foreach (var rowIndex in request.RowIndexes)
        {
            var existing = assignments.FindIndex(a => a.RowIndex == rowIndex);
            if (existing >= 0)
            {
                assignments[existing] = new OwnerAssignment(rowIndex, request.OwnerId);
            }
            else
            {
                assignments.Add(new OwnerAssignment(rowIndex, request.OwnerId));
            }
        }

        var updated = new TriageResolutionDto
        {
            OwnerAssignments = assignments.AsReadOnly(),
            StageResolutions = resolution.StageResolutions,
            PartnerPctResolutions = resolution.PartnerPctResolutions,
            DedupeDecisions = resolution.DedupeDecisions,
        };
        job.SetTriageResolution(JsonSerializer.Serialize(updated), _clock.UtcNow);
        await _repository.UpdateAsync(job, cancellationToken);

        return new BulkAssignOwnerResult(request.RowIndexes.Count);
    }
}
