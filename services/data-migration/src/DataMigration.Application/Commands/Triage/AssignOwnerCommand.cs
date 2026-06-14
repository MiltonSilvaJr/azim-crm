using System.Text.Json;
using DataMigration.Application.DTOs;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using MediatR;

namespace DataMigration.Application.Commands.Triage;

/// <summary>Resultado da atribuição de owner.</summary>
/// <param name="RowIndex">Índice da linha atualizada.</param>
/// <param name="OwnerId">ID do usuário atribuído.</param>
public sealed record AssignOwnerResult(int RowIndex, Guid OwnerId);

/// <summary>
/// Command de atribuição individual de owner a uma oportunidade triada.
///
/// Rastreia: design §5.1, Req 4.1, TASK-10.
/// </summary>
public sealed record AssignOwnerCommand(
    Guid JobId,
    int RowIndex,
    Guid OwnerId) : IRequest<AssignOwnerResult>;

/// <summary>
/// Handler de atribuição individual de owner.
/// Persiste o snapshot atualizado da triagem no job (DD-007).
///
/// Rastreia: design §5.1, Req 4.1, DD-007, TASK-10.
/// </summary>
public sealed class AssignOwnerHandler : IRequestHandler<AssignOwnerCommand, AssignOwnerResult>
{
    private readonly IMigrationJobRepository _repository;
    private readonly IClock _clock;

    /// <summary>Cria o handler com as dependências injetadas.</summary>
    public AssignOwnerHandler(IMigrationJobRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<AssignOwnerResult> Handle(
        AssignOwnerCommand request,
        CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new MigrationDomainException("MIG-ERR-004", $"Job não encontrado: {request.JobId}.");

        // Carrega ou inicializa o snapshot de triagem
        var resolution = LoadOrNew(job.TriageResolutionJson);

        // Atualiza (ou adiciona) o owner para o índice de linha
        var assignments = resolution.OwnerAssignments.ToList();
        var existing = assignments.FindIndex(a => a.RowIndex == request.RowIndex);
        if (existing >= 0)
        {
            assignments[existing] = new OwnerAssignment(request.RowIndex, request.OwnerId);
        }
        else
        {
            assignments.Add(new OwnerAssignment(request.RowIndex, request.OwnerId));
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

        return new AssignOwnerResult(request.RowIndex, request.OwnerId);
    }

    internal static TriageResolutionDto LoadOrNew(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new TriageResolutionDto()
            : JsonSerializer.Deserialize<TriageResolutionDto>(json) ?? new TriageResolutionDto();
}
