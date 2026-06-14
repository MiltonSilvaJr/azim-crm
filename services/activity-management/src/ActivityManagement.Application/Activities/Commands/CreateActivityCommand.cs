namespace ActivityManagement.Application.Activities.Commands;

using ActivityManagement.Application.Behaviors;
using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Repositories;
using ActivityManagement.Domain.Activities.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

/// <summary>
/// Command para criar uma nova atividade comercial (Req 1).
/// Valida vínculos de oportunidade/conta via portas de leitura (Req 3.2/3.3).
/// Publica <c>ActivityCreated</c> via Outbox no commit transacional.
/// Mapeia: design §5.1, Req 1, Req 3, ACT-ERR-001/002/005/006/010, TASK-07.
/// </summary>
/// <param name="Type">Tipo da atividade (meeting|follow_up|call|email|task).</param>
/// <param name="Title">Título não vazio (I1).</param>
/// <param name="DueAt">Instante de vencimento.</param>
/// <param name="OwnerId">Usuário responsável pela atividade.</param>
/// <param name="Priority">Prioridade; usa medium quando nula (I3).</param>
/// <param name="Description">Descrição opcional (PII potencial — nunca logada).</param>
/// <param name="OpportunityId">Vínculo opcional com oportunidade do mesmo tenant.</param>
/// <param name="AccountId">Vínculo opcional com conta do mesmo tenant.</param>
public sealed record CreateActivityCommand(
    string          Type,
    string          Title,
    DateTimeOffset  DueAt,
    Guid            OwnerId,
    string?         Priority      = null,
    string?         Description   = null,
    Guid?           OpportunityId = null,
    Guid?           AccountId     = null)
    : IRequest<Guid>, ITenantRequest, IRequireWriteRole, ITransactionalCommand
{
    /// <inheritdoc />
    public TenantContext? TenantContext { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<Domain.Activities.Events.DomainEvent> DomainEvents { get; private set; } = [];

    // Preenchido pelo handler após criação do agregado
    internal void SetDomainEvents(IReadOnlyList<Domain.Activities.Events.DomainEvent> events)
        => DomainEvents = events;
}

/// <summary>
/// Handler do <see cref="CreateActivityCommand"/>.
/// Valida vínculos, invoca <c>Activity.Create</c> e persiste via repositório.
/// Mapeia: design §5.1, design §5.3, TASK-07.
/// </summary>
internal sealed class CreateActivityCommandHandler : IRequestHandler<CreateActivityCommand, Guid>
{
    private readonly IActivityRepository  _repository;
    private readonly IOpportunityReadPort _opportunityPort;
    private readonly IAccountReadPort     _accountPort;
    private readonly IClock               _clock;
    private readonly IActivityMetrics     _metrics;
    private readonly ILogger<CreateActivityCommandHandler> _logger;

    public CreateActivityCommandHandler(
        IActivityRepository  repository,
        IOpportunityReadPort opportunityPort,
        IAccountReadPort     accountPort,
        IClock               clock,
        IActivityMetrics     metrics,
        ILogger<CreateActivityCommandHandler> logger)
    {
        _repository      = repository;
        _opportunityPort = opportunityPort;
        _accountPort     = accountPort;
        _clock           = clock;
        _metrics         = metrics;
        _logger          = logger;
    }

    public async Task<Guid> Handle(CreateActivityCommand request, CancellationToken cancellationToken)
    {
        var ctx = request.TenantContext!;

        // Validar vínculo de oportunidade (mesmo tenant — anti-enumeração: ACT-ERR-005)
        OpportunityLink? opportunityLink = null;
        if (request.OpportunityId.HasValue)
        {
            var exists = await _opportunityPort.ExistsAsync(
                request.OpportunityId.Value, ctx.TenantId, cancellationToken);
            if (!exists)
                throw new OpportunityNotFoundException(request.OpportunityId.Value);

            opportunityLink = OpportunityLink.Create(request.OpportunityId.Value);
        }

        // Validar vínculo de conta (mesmo tenant — anti-enumeração: ACT-ERR-006)
        AccountLink? accountLink = null;
        if (request.AccountId.HasValue)
        {
            var exists = await _accountPort.ExistsAsync(
                request.AccountId.Value, ctx.TenantId, cancellationToken);
            if (!exists)
                throw new AccountNotFoundException(request.AccountId.Value);

            accountLink = AccountLink.Create(request.AccountId.Value);
        }

        var now = _clock.UtcNow;

        var activity = Activity.Create(
            tenantId:        ctx.TenantId,
            buId:            ctx.BuId,
            ownerId:         request.OwnerId,
            type:            ActivityType.Create(request.Type),
            title:           request.Title,
            dueAt:           DueDate.Create(request.DueAt),
            now:             now,
            priority:        request.Priority is not null ? Priority.Create(request.Priority) : null,
            description:     request.Description,
            opportunityLink: opportunityLink,
            accountLink:     accountLink,
            correlationId:   ctx.CorrelationId);

        await _repository.SaveAsync(activity, cancellationToken);
        request.SetDomainEvents(activity.DomainEvents);

        // Métrica: atividade criada (RNF 6.2, design §11)
        _metrics.IncrementCreated();

        // Log estruturado sem title/description (RNF 7.2)
        _logger.LogInformation(
            "Atividade criada activity_id={ActivityId} tenant_id={TenantId} owner_id={OwnerId} type={Type}",
            activity.Id,
            ctx.TenantId,
            request.OwnerId,
            request.Type);

        return activity.Id;
    }
}

// ── Exceções de aplicação para vínculos inválidos ────────────────────────────

/// <summary>Oportunidade não encontrada ou de outro tenant (ACT-ERR-005).</summary>
public sealed class OpportunityNotFoundException : Exception
{
    /// <summary>ID da oportunidade não encontrada.</summary>
    public Guid OpportunityId { get; }

    /// <summary>Inicializa com o ID da oportunidade.</summary>
    public OpportunityNotFoundException(Guid opportunityId)
        : base($"Oportunidade '{opportunityId}' não encontrada ou inacessível.")
    {
        OpportunityId = opportunityId;
    }
}

/// <summary>Conta não encontrada ou de outro tenant (ACT-ERR-006).</summary>
public sealed class AccountNotFoundException : Exception
{
    /// <summary>ID da conta não encontrada.</summary>
    public Guid AccountId { get; }

    /// <summary>Inicializa com o ID da conta.</summary>
    public AccountNotFoundException(Guid accountId)
        : base($"Conta '{accountId}' não encontrada ou inacessível.")
    {
        AccountId = accountId;
    }
}

/// <summary>Atividade não encontrada ou inacessível no tenant/escopo (ACT-ERR-003).</summary>
public sealed class ActivityNotFoundException : Exception
{
    /// <summary>ID da atividade não encontrada.</summary>
    public Guid ActivityId { get; }

    /// <summary>Inicializa com o ID da atividade.</summary>
    public ActivityNotFoundException(Guid activityId)
        : base($"Atividade '{activityId}' não encontrada.")
    {
        ActivityId = activityId;
    }
}
