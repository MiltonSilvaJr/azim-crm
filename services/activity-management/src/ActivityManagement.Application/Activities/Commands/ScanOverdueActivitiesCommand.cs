namespace ActivityManagement.Application.Activities.Commands;

using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities.Events;
using ActivityManagement.Domain.Activities.Repositories;
using MediatR;

/// <summary>
/// Command para varredura de atividades vencidas.
/// Acionado diariamente pelo Cloud Scheduler (TRD §scheduler <c>activity-overdue</c>).
/// Itera atividades vencidas em lotes paginados e enfileira <see cref="ActivityOverdue"/>
/// no Outbox com chave de deduplicação <c>(activityId, scanDate)</c> — DD-005, Req 14.3.
/// <b>Não altera o status das atividades</b> — scan é somente leitura (DD-005).
/// Mapeia: design §5.1, Req 14, DD-005, TASK-12.
/// </summary>
public sealed record ScanOverdueActivitiesCommand : IRequest;

/// <summary>
/// Handler do <see cref="ScanOverdueActivitiesCommand"/>.
/// </summary>
internal sealed class ScanOverdueActivitiesCommandHandler : IRequestHandler<ScanOverdueActivitiesCommand>
{
    private const int BatchSize = 100;

    private readonly IActivityRepository _repository;
    private readonly IOutboxPublisher    _outbox;
    private readonly IClock              _clock;

    public ScanOverdueActivitiesCommandHandler(
        IActivityRepository repository,
        IOutboxPublisher    outbox,
        IClock              clock)
    {
        _repository = repository;
        _outbox     = outbox;
        _clock      = clock;
    }

    public async Task Handle(ScanOverdueActivitiesCommand request, CancellationToken cancellationToken)
    {
        var now      = _clock.UtcNow;
        var scanDate = DateOnly.FromDateTime(now.UtcDateTime);
        var skip     = 0;

        while (true)
        {
            var batch = await _repository.GetOverduePageAsync(now, skip, BatchSize, cancellationToken);

            if (batch.Count == 0)
                break;

            foreach (var activity in batch)
            {
                // Não altera o status da atividade (DD-005)
                var correlationId    = Guid.NewGuid();
                var deduplicationKey = $"{activity.Id}:{scanDate:yyyy-MM-dd}";

                var @event = new ActivityOverdue(
                    EventId:       Guid.NewGuid(),
                    OccurredAt:    now,
                    ActivityId:    activity.Id,
                    TenantId:      activity.TenantId,
                    OwnerId:       activity.OwnerId,
                    DueAt:         activity.DueAt.Value,
                    ScanDate:      scanDate,
                    CorrelationId: correlationId,
                    OpportunityId: activity.OpportunityLink?.OpportunityId);

                await _outbox.EnqueueAsync(@event, deduplicationKey, cancellationToken);
            }

            skip += batch.Count;

            if (batch.Count < BatchSize)
                break;
        }
    }
}
