using System.Text.Json;
using DataMigration.Application.DTOs;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using MediatR;

namespace DataMigration.Application.Commands.Triage;

/// <summary>Resultado da resolução de parceiro.</summary>
/// <param name="RowIndex">Índice da linha atualizada.</param>
/// <param name="PartnerId">ID do parceiro escolhido (nulo = sem parceiro).</param>
public sealed record ResolvePartnerPctResult(int RowIndex, Guid? PartnerId);

/// <summary>
/// Command de resolução de parceiro/percentual pendente.
/// Não bloqueante — parceiro sem percentual não impede o import.
///
/// Rastreia: design §5.1, Req 5.2, TASK-10.
/// </summary>
public sealed record ResolvePartnerPctCommand(
    Guid JobId,
    int RowIndex,
    Guid? PartnerId) : IRequest<ResolvePartnerPctResult>;

/// <summary>
/// Handler de resolução de parceiro/percentual pendente.
///
/// Rastreia: design §5.1, Req 5.2, DD-007, TASK-10.
/// </summary>
public sealed class ResolvePartnerPctHandler
    : IRequestHandler<ResolvePartnerPctCommand, ResolvePartnerPctResult>
{
    private readonly IMigrationJobRepository _repository;
    private readonly IClock _clock;

    /// <summary>Cria o handler com as dependências injetadas.</summary>
    public ResolvePartnerPctHandler(IMigrationJobRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<ResolvePartnerPctResult> Handle(
        ResolvePartnerPctCommand request,
        CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new MigrationDomainException("MIG-ERR-004", $"Job não encontrado: {request.JobId}.");

        var resolution = AssignOwnerHandler.LoadOrNew(job.TriageResolutionJson);
        var partners = resolution.PartnerPctResolutions.ToList();

        var existing = partners.FindIndex(p => p.RowIndex == request.RowIndex);
        if (existing >= 0)
        {
            partners[existing] = new PartnerPctResolution(request.RowIndex, request.PartnerId);
        }
        else
        {
            partners.Add(new PartnerPctResolution(request.RowIndex, request.PartnerId));
        }

        var updated = new TriageResolutionDto
        {
            OwnerAssignments = resolution.OwnerAssignments,
            StageResolutions = resolution.StageResolutions,
            PartnerPctResolutions = partners.AsReadOnly(),
            DedupeDecisions = resolution.DedupeDecisions,
        };
        job.SetTriageResolution(JsonSerializer.Serialize(updated), _clock.UtcNow);
        await _repository.UpdateAsync(job, cancellationToken);

        return new ResolvePartnerPctResult(request.RowIndex, request.PartnerId);
    }
}
