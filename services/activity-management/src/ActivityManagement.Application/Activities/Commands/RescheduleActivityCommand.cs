namespace ActivityManagement.Application.Activities.Commands;

using ActivityManagement.Application.Behaviors;
using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities.Repositories;
using ActivityManagement.Domain.Activities.ValueObjects;
using MediatR;

/// <summary>
/// Resultado do reagendamento de uma atividade.
/// </summary>
/// <param name="ActivityId">ID da atividade reagendada.</param>
/// <param name="NewDueAt">Nova data de vencimento efetiva.</param>
public sealed record RescheduleActivityResult(
    Guid           ActivityId,
    DateTimeOffset NewDueAt);

/// <summary>
/// Command para reagendamento direto de uma atividade via JWT (Req 8).
/// O reagendamento via token do digest é tratado pelo <see cref="ProcessDigestActionCommand"/>
/// com <c>action=reschedule</c> (Req 8.5, design §5.1).
/// Mapeia: design §5.1, Req 8, ACT-ERR-011, TASK-10.
/// </summary>
/// <param name="ActivityId">Identificador da atividade a reagendar.</param>
/// <param name="NewDueAt">Nova data de vencimento.</param>
public sealed record RescheduleActivityCommand(Guid ActivityId, DateTimeOffset NewDueAt)
    : IRequest<RescheduleActivityResult>, ITenantRequest, IRequireWriteRole, ITransactionalCommand
{
    /// <inheritdoc />
    public TenantContext? TenantContext { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<Domain.Activities.Events.DomainEvent> DomainEvents { get; private set; } = [];

    internal void SetDomainEvents(IReadOnlyList<Domain.Activities.Events.DomainEvent> events)
        => DomainEvents = events;
}

/// <summary>
/// Handler do <see cref="RescheduleActivityCommand"/>.
/// Mapeia: design §5.1, Req 8, TASK-10.
/// </summary>
internal sealed class RescheduleActivityCommandHandler
    : IRequestHandler<RescheduleActivityCommand, RescheduleActivityResult>
{
    private readonly IActivityRepository _repository;
    private readonly IAuditPublisher     _auditPublisher;
    private readonly IClock              _clock;

    public RescheduleActivityCommandHandler(
        IActivityRepository repository,
        IAuditPublisher     auditPublisher,
        IClock              clock)
    {
        _repository     = repository;
        _auditPublisher = auditPublisher;
        _clock          = clock;
    }

    public async Task<RescheduleActivityResult> Handle(
        RescheduleActivityCommand request,
        CancellationToken         cancellationToken)
    {
        var ctx = request.TenantContext!;
        var now = _clock.UtcNow;

        var activity = await _repository.FindByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new ActivityNotFoundException(request.ActivityId);

        var previousDueAt = activity.DueAt.Value;

        // Reagenda — lança ActivityTerminalException se terminal (ACT-ERR-011, I6)
        activity.Reschedule(DueDate.Create(request.NewDueAt), now);

        await _repository.SaveAsync(activity, cancellationToken);
        request.SetDomainEvents(activity.DomainEvents);

        // Auditoria com delta da dueAt
        var deltaJson = $"{{\"dueAt\":{{\"from\":\"{previousDueAt:O}\",\"to\":\"{request.NewDueAt:O}\"}}}}";
        await _auditPublisher.PublishAsync(new AuditEntry(
            TenantId:      ctx.TenantId,
            UserId:        ctx.UserId,
            EntityType:    "Activity",
            EntityId:      activity.Id,
            Action:        "rescheduled",
            DeltaJson:     deltaJson,
            CorrelationId: ctx.CorrelationId), cancellationToken);

        return new RescheduleActivityResult(
            ActivityId: activity.Id,
            NewDueAt:   activity.DueAt.Value);
    }
}
