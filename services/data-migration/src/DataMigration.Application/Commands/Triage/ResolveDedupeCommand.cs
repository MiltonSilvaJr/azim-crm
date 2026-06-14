using System.Text.Json;
using DataMigration.Application.DTOs;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using MediatR;

namespace DataMigration.Application.Commands.Triage;

/// <summary>Resultado da resolução de dedupe.</summary>
/// <param name="RowIndexA">Índice da primeira linha.</param>
/// <param name="RowIndexB">Índice da segunda linha.</param>
/// <param name="MergeIntoRowA">Verdadeiro para mesclar B em A; falso para manter ambas.</param>
public sealed record ResolveDedupeResult(int RowIndexA, int RowIndexB, bool MergeIntoRowA);

/// <summary>
/// Command de resolução de par candidato a dedupe.
/// Decisão humana: merge ou manutenção de ambas as contas (RN-014).
///
/// Rastreia: design §5.1, Req 7.2, RN-014, TASK-10.
/// </summary>
public sealed record ResolveDedupeCommand(
    Guid JobId,
    int RowIndexA,
    int RowIndexB,
    bool MergeIntoRowA) : IRequest<ResolveDedupeResult>;

/// <summary>
/// Handler de resolução de dedupe.
///
/// Rastreia: design §5.1, Req 7.2, DD-007, RN-014, TASK-10.
/// </summary>
public sealed class ResolveDedupeHandler
    : IRequestHandler<ResolveDedupeCommand, ResolveDedupeResult>
{
    private readonly IMigrationJobRepository _repository;
    private readonly IClock _clock;

    /// <summary>Cria o handler com as dependências injetadas.</summary>
    public ResolveDedupeHandler(IMigrationJobRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<ResolveDedupeResult> Handle(
        ResolveDedupeCommand request,
        CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new MigrationDomainException("MIG-ERR-004", $"Job não encontrado: {request.JobId}.");

        var resolution = AssignOwnerHandler.LoadOrNew(job.TriageResolutionJson);
        var dedupes = resolution.DedupeDecisions.ToList();

        var existing = dedupes.FindIndex(
            d => d.RowIndexA == request.RowIndexA && d.RowIndexB == request.RowIndexB);

        if (existing >= 0)
        {
            dedupes[existing] = new DedupeDecision(
                request.RowIndexA, request.RowIndexB, request.MergeIntoRowA);
        }
        else
        {
            dedupes.Add(new DedupeDecision(
                request.RowIndexA, request.RowIndexB, request.MergeIntoRowA));
        }

        var updated = new TriageResolutionDto
        {
            OwnerAssignments = resolution.OwnerAssignments,
            StageResolutions = resolution.StageResolutions,
            PartnerPctResolutions = resolution.PartnerPctResolutions,
            DedupeDecisions = dedupes.AsReadOnly(),
        };
        job.SetTriageResolution(JsonSerializer.Serialize(updated), _clock.UtcNow);
        await _repository.UpdateAsync(job, cancellationToken);

        return new ResolveDedupeResult(request.RowIndexA, request.RowIndexB, request.MergeIntoRowA);
    }
}
