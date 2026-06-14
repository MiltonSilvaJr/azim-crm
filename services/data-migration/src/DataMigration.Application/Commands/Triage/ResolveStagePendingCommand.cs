using System.Text.Json;
using DataMigration.Application.DTOs;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using MediatR;

namespace DataMigration.Application.Commands.Triage;

/// <summary>Resultado da resolução de estágio.</summary>
/// <param name="RowIndex">Índice da linha atualizada.</param>
/// <param name="StageId">ID do estágio escolhido.</param>
public sealed record ResolveStagePendingResult(int RowIndex, Guid StageId);

/// <summary>
/// Command de resolução de estágio faltante para uma linha.
/// Não bloqueante — fallback "Lead" já aplicado pelo StageFallbackPolicy.
///
/// Rastreia: design §5.1, Req 5.1, TASK-10.
/// </summary>
public sealed record ResolveStagePendingCommand(
    Guid JobId,
    int RowIndex,
    Guid StageId) : IRequest<ResolveStagePendingResult>;

/// <summary>
/// Handler de resolução de estágio pendente.
///
/// Rastreia: design §5.1, Req 5.1, DD-007, TASK-10.
/// </summary>
public sealed class ResolveStagePendingHandler
    : IRequestHandler<ResolveStagePendingCommand, ResolveStagePendingResult>
{
    private readonly IMigrationJobRepository _repository;
    private readonly IClock _clock;

    /// <summary>Cria o handler com as dependências injetadas.</summary>
    public ResolveStagePendingHandler(IMigrationJobRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<ResolveStagePendingResult> Handle(
        ResolveStagePendingCommand request,
        CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new MigrationDomainException("MIG-ERR-004", $"Job não encontrado: {request.JobId}.");

        var resolution = AssignOwnerHandler.LoadOrNew(job.TriageResolutionJson);
        var stages = resolution.StageResolutions.ToList();

        var existing = stages.FindIndex(s => s.RowIndex == request.RowIndex);
        if (existing >= 0)
        {
            stages[existing] = new StageResolution(request.RowIndex, request.StageId);
        }
        else
        {
            stages.Add(new StageResolution(request.RowIndex, request.StageId));
        }

        var updated = new TriageResolutionDto
        {
            OwnerAssignments = resolution.OwnerAssignments,
            StageResolutions = stages.AsReadOnly(),
            PartnerPctResolutions = resolution.PartnerPctResolutions,
            DedupeDecisions = resolution.DedupeDecisions,
        };
        job.SetTriageResolution(JsonSerializer.Serialize(updated), _clock.UtcNow);
        await _repository.UpdateAsync(job, cancellationToken);

        return new ResolveStagePendingResult(request.RowIndex, request.StageId);
    }
}
