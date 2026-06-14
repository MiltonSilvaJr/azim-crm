namespace ActivityManagement.Application.Activities.Commands;

using ActivityManagement.Application.Behaviors;
using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities.Repositories;
using MediatR;

/// <summary>
/// Command para excluir uma atividade (Req 2.3).
/// Registra auditoria com <c>action=deleted</c> via <see cref="IAuditPublisher"/>.
/// Mapeia: design §5.1, Req 2.3, ACT-ERR-003/007, TASK-07.
/// </summary>
/// <param name="ActivityId">Identificador da atividade a excluir.</param>
public sealed record DeleteActivityCommand(Guid ActivityId)
    : IRequest, ITenantRequest, IRequireWriteRole, ITransactionalCommand
{
    /// <inheritdoc />
    public TenantContext? TenantContext { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<Domain.Activities.Events.DomainEvent> DomainEvents { get; } = [];
}

/// <summary>
/// Handler do <see cref="DeleteActivityCommand"/>.
/// Mapeia: design §5.1, design §5.3, TASK-07.
/// </summary>
internal sealed class DeleteActivityCommandHandler : IRequestHandler<DeleteActivityCommand>
{
    private readonly IActivityRepository _repository;
    private readonly IAuditPublisher     _auditPublisher;

    public DeleteActivityCommandHandler(
        IActivityRepository repository,
        IAuditPublisher     auditPublisher)
    {
        _repository     = repository;
        _auditPublisher = auditPublisher;
    }

    public async Task Handle(DeleteActivityCommand request, CancellationToken cancellationToken)
    {
        var ctx = request.TenantContext!;

        var activity = await _repository.FindByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new ActivityNotFoundException(request.ActivityId);

        await _repository.DeleteAsync(activity.Id, cancellationToken);

        await _auditPublisher.PublishAsync(new AuditEntry(
            TenantId:      ctx.TenantId,
            UserId:        ctx.UserId,
            EntityType:    "Activity",
            EntityId:      activity.Id,
            Action:        "deleted",
            DeltaJson:     "{}",
            CorrelationId: ctx.CorrelationId), cancellationToken);
    }
}
