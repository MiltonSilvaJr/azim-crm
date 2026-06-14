namespace ActivityManagement.Application.Activities.Commands;

using ActivityManagement.Application.Behaviors;
using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

/// <summary>
/// Resultado da conclusão de uma atividade.
/// Inclui flag de idempotência e sugestão de vínculo quando aplicável (Req 6.5, Req 9).
/// </summary>
/// <param name="ActivityId">ID da atividade concluída.</param>
/// <param name="CompletedAt">Instante de conclusão efetiva (o original, não o da chamada idempotente).</param>
/// <param name="WasAlreadyCompleted">Verdadeiro quando a atividade já estava concluída (idempotência — DD-004).</param>
public sealed record CompleteActivityResult(
    Guid            ActivityId,
    DateTimeOffset  CompletedAt,
    bool            WasAlreadyCompleted);

/// <summary>
/// Command para concluir uma atividade em um clique (Req 6).
/// Idempotente: N chamadas com a mesma <paramref name="ActivityId"/> produzem exatamente
/// um <c>ActivityCompleted</c> e um <c>completedAt</c> (DD-004, PBT-02).
/// Mapeia: design §5.1, Req 6, RNF 3, DD-004, PBT-02, ACT-ERR-003/004, TASK-08.
/// </summary>
/// <param name="ActivityId">Identificador da atividade a concluir.</param>
public sealed record CompleteActivityCommand(Guid ActivityId)
    : IRequest<CompleteActivityResult>, ITenantRequest, IRequireWriteRole, ITransactionalCommand
{
    /// <inheritdoc />
    public TenantContext? TenantContext { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<Domain.Activities.Events.DomainEvent> DomainEvents { get; private set; } = [];

    internal void SetDomainEvents(IReadOnlyList<Domain.Activities.Events.DomainEvent> events)
        => DomainEvents = events;
}

/// <summary>
/// Handler do <see cref="CompleteActivityCommand"/>.
/// Mapeia: design §5.1/§5.3, DD-004, PBT-02, TASK-08.
/// </summary>
internal sealed class CompleteActivityCommandHandler
    : IRequestHandler<CompleteActivityCommand, CompleteActivityResult>
{
    private readonly IActivityRepository _repository;
    private readonly IAuditPublisher     _auditPublisher;
    private readonly IClock              _clock;
    private readonly IActivityMetrics    _metrics;
    private readonly ILogger<CompleteActivityCommandHandler> _logger;

    public CompleteActivityCommandHandler(
        IActivityRepository repository,
        IAuditPublisher     auditPublisher,
        IClock              clock,
        IActivityMetrics    metrics,
        ILogger<CompleteActivityCommandHandler> logger)
    {
        _repository     = repository;
        _auditPublisher = auditPublisher;
        _clock          = clock;
        _metrics        = metrics;
        _logger         = logger;
    }

    public async Task<CompleteActivityResult> Handle(
        CompleteActivityCommand request,
        CancellationToken       cancellationToken)
    {
        var ctx = request.TenantContext!;

        var activity = await _repository.FindByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new ActivityNotFoundException(request.ActivityId);

        var wasAlreadyCompleted = activity.Status == Domain.Activities.ValueObjects.ActivityStatus.Completed;

        var now = _clock.UtcNow;

        // Activity.Complete é idempotente: no-op sem novo evento se já completed (DD-004)
        activity.Complete(now, ctx.CorrelationId);

        if (!wasAlreadyCompleted)
        {
            await _repository.SaveAsync(activity, cancellationToken);
            request.SetDomainEvents(activity.DomainEvents);

            await _auditPublisher.PublishAsync(new AuditEntry(
                TenantId:      ctx.TenantId,
                UserId:        ctx.UserId,
                EntityType:    "Activity",
                EntityId:      activity.Id,
                Action:        "completed",
                DeltaJson:     "{}",
                CorrelationId: ctx.CorrelationId), cancellationToken);

            // Métrica: conclusão efetiva (RNF 6.2, design §11) — não conta idempotente
            _metrics.IncrementCompleted();

            _logger.LogInformation(
                "Atividade concluída activity_id={ActivityId} tenant_id={TenantId}",
                activity.Id,
                ctx.TenantId);
        }

        return new CompleteActivityResult(
            ActivityId:          activity.Id,
            CompletedAt:         activity.CompletedAt!.Value,
            WasAlreadyCompleted: wasAlreadyCompleted);
    }
}
