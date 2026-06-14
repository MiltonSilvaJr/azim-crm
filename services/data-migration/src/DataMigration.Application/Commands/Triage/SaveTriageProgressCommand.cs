using System.Text.Json;
using DataMigration.Application.DTOs;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using MediatR;

namespace DataMigration.Application.Commands.Triage;

/// <summary>Resultado do salvamento de progresso da triagem.</summary>
/// <param name="Saved">Verdadeiro quando persistido com sucesso.</param>
public sealed record SaveTriageProgressResult(bool Saved);

/// <summary>
/// Command de salvamento do progresso da triagem entre sessões.
/// Persiste snapshot JSONB sem PII (DD-007, Req 4.4).
///
/// Rastreia: design §5.1, Req 4.4, DD-007, TASK-10.
/// </summary>
public sealed record SaveTriageProgressCommand(
    Guid JobId,
    TriageResolutionDto Resolution) : IRequest<SaveTriageProgressResult>;

/// <summary>
/// Handler de salvamento do progresso da triagem.
///
/// Garante ausência de PII no snapshot (RNF 3, DD-007).
///
/// Rastreia: design §5.1, Req 4.4, DD-007, RNF 3, TASK-10.
/// </summary>
public sealed class SaveTriageProgressHandler
    : IRequestHandler<SaveTriageProgressCommand, SaveTriageProgressResult>
{
    private readonly IMigrationJobRepository _repository;
    private readonly IClock _clock;

    /// <summary>Cria o handler com as dependências injetadas.</summary>
    public SaveTriageProgressHandler(IMigrationJobRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<SaveTriageProgressResult> Handle(
        SaveTriageProgressCommand request,
        CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new MigrationDomainException("MIG-ERR-004", $"Job não encontrado: {request.JobId}.");

        // Snapshot contém apenas IDs e índices de linha — sem PII (DD-007, RNF 3)
        var json = JsonSerializer.Serialize(request.Resolution);
        job.SetTriageResolution(json, _clock.UtcNow);
        await _repository.UpdateAsync(job, cancellationToken);

        return new SaveTriageProgressResult(Saved: true);
    }
}
