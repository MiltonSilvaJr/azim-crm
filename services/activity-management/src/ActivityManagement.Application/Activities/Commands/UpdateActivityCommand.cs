namespace ActivityManagement.Application.Activities.Commands;

using ActivityManagement.Application.Behaviors;
using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities.Repositories;
using ActivityManagement.Domain.Activities.ValueObjects;
using MediatR;

/// <summary>
/// Command para atualizar os atributos de uma atividade existente (Req 2).
/// Guarda atividade terminal (I6 — ACT-ERR-011).
/// Mapeia: design §5.1, Req 2, ACT-ERR-001/002/003/005/006/011, TASK-07.
/// </summary>
/// <param name="ActivityId">Identificador da atividade a atualizar.</param>
/// <param name="Title">Novo título não vazio.</param>
/// <param name="DueAt">Nova data de vencimento.</param>
/// <param name="Priority">Nova prioridade (opcional — mantém a atual quando nula).</param>
/// <param name="Description">Nova descrição (opcional — texto livre, PII potencial).</param>
/// <param name="OpportunityId">Novo vínculo com oportunidade (opcional).</param>
/// <param name="AccountId">Novo vínculo com conta (opcional).</param>
public sealed record UpdateActivityCommand(
    Guid            ActivityId,
    string          Title,
    DateTimeOffset  DueAt,
    string?         Priority      = null,
    string?         Description   = null,
    Guid?           OpportunityId = null,
    Guid?           AccountId     = null)
    : IRequest, ITenantRequest, IRequireWriteRole, ITransactionalCommand
{
    /// <inheritdoc />
    public TenantContext? TenantContext { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<Domain.Activities.Events.DomainEvent> DomainEvents { get; } = [];
}

/// <summary>
/// Handler do <see cref="UpdateActivityCommand"/>.
/// Mapeia: design §5.1, design §5.3, TASK-07.
/// </summary>
internal sealed class UpdateActivityCommandHandler : IRequestHandler<UpdateActivityCommand>
{
    private readonly IActivityRepository  _repository;
    private readonly IOpportunityReadPort _opportunityPort;
    private readonly IAccountReadPort     _accountPort;
    private readonly IClock               _clock;

    public UpdateActivityCommandHandler(
        IActivityRepository  repository,
        IOpportunityReadPort opportunityPort,
        IAccountReadPort     accountPort,
        IClock               clock)
    {
        _repository      = repository;
        _opportunityPort = opportunityPort;
        _accountPort     = accountPort;
        _clock           = clock;
    }

    public async Task Handle(UpdateActivityCommand request, CancellationToken cancellationToken)
    {
        var ctx = request.TenantContext!;

        var activity = await _repository.FindByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new ActivityNotFoundException(request.ActivityId);

        // Validar vínculo de oportunidade quando informado
        if (request.OpportunityId.HasValue)
        {
            var exists = await _opportunityPort.ExistsAsync(
                request.OpportunityId.Value, ctx.TenantId, cancellationToken);
            if (!exists)
                throw new OpportunityNotFoundException(request.OpportunityId.Value);
        }

        // Validar vínculo de conta quando informado
        if (request.AccountId.HasValue)
        {
            var exists = await _accountPort.ExistsAsync(
                request.AccountId.Value, ctx.TenantId, cancellationToken);
            if (!exists)
                throw new AccountNotFoundException(request.AccountId.Value);
        }

        var now = _clock.UtcNow;

        // Delegação ao domínio (I6 lança ActivityTerminalException — ACT-ERR-011)
        activity.UpdateDetails(
            title:       request.Title,
            description: request.Description,
            dueAt:       DueDate.Create(request.DueAt),
            priority:    request.Priority is not null ? Priority.Create(request.Priority) : null,
            now:         now);

        await _repository.SaveAsync(activity, cancellationToken);
    }
}
