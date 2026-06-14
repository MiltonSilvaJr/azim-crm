namespace ActivityManagement.Domain.Activities;

using ActivityManagement.Domain.Activities.Events;
using ActivityManagement.Domain.Activities.Exceptions;
using ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Aggregate Root do módulo activity-management.
/// Encapsula o ciclo de vida de uma atividade comercial: criação, atualização,
/// conclusão, cancelamento e reagendamento — todos via métodos de comportamento
/// com invariantes enforçadas (I1–I6, design §4.1).
///
/// Invariantes:
///   I1 — título não vazio após trim → <see cref="TitleRequiredException"/>
///   I2 — type ∈ lista canônica → <see cref="InvalidActivityTypeException"/>
///   I3 — status=pending e priority=medium quando não informados
///   I4 — transições de status conforme a máquina de estados → <see cref="InvalidStatusTransitionException"/>
///   I5 — completedAt preenchido sse status=completed
///   I6 — atividade terminal rejeita toda escrita → <see cref="ActivityTerminalException"/>
///
/// Sem setters públicos; estado mutável apenas via métodos de comportamento.
/// Domain events acumulados em <see cref="DomainEvents"/> para despacho pós-commit.
/// Mapeia: design §4.1, Req 1–6, Req 8, DD-004, TASK-04.
/// </summary>
public sealed class Activity
{
    private readonly List<DomainEvent> _domainEvents = [];

    // ── Construtor privado — uso exclusivo das factories ─────────────────────

    private Activity(
        Guid             id,
        Guid             tenantId,
        Guid             buId,
        Guid             ownerId,
        ActivityType     type,
        string           title,
        DueDate          dueAt,
        ActivityStatus   status,
        Priority         priority,
        DateTimeOffset?  completedAt,
        string?          description,
        OpportunityLink? opportunityLink,
        AccountLink?     accountLink,
        DateTimeOffset   createdAt,
        DateTimeOffset   updatedAt)
    {
        Id              = id;
        TenantId        = tenantId;
        BuId            = buId;
        OwnerId         = ownerId;
        Type            = type;
        Title           = title;
        DueAt           = dueAt;
        Status          = status;
        Priority        = priority;
        CompletedAt     = completedAt;
        Description     = description;
        OpportunityLink = opportunityLink;
        AccountLink     = accountLink;
        CreatedAt       = createdAt;
        UpdatedAt       = updatedAt;
    }

    // ── Propriedades de identidade e estado ──────────────────────────────────

    /// <summary>Identificador único da atividade (UUID).</summary>
    public Guid Id { get; }

    /// <summary>Tenant ao qual a atividade pertence (RNF 1, ADR-0001).</summary>
    public Guid TenantId { get; }

    /// <summary>Business Unit ao qual a atividade pertence (Req 1.7).</summary>
    public Guid BuId { get; }

    /// <summary>Usuário responsável pela atividade (Req 1.1).</summary>
    public Guid OwnerId { get; }

    /// <summary>Tipo da atividade (meeting, follow_up, call, email, task — I2).</summary>
    public ActivityType Type { get; }

    /// <summary>Título da atividade (não vazio — I1).</summary>
    public string Title { get; private set; }

    /// <summary>Descrição opcional da atividade (texto livre — PII potencial, RNF 7).</summary>
    public string? Description { get; private set; }

    /// <summary>Instante de vencimento (TIMESTAMPTZ, base das faixas de visão).</summary>
    public DueDate DueAt { get; private set; }

    /// <summary>Status corrente na máquina de estados (I3, I4, I5, I6).</summary>
    public ActivityStatus Status { get; private set; }

    /// <summary>Prioridade da atividade (default: medium — I3).</summary>
    public Priority Priority { get; private set; }

    /// <summary>Instante de conclusão efetiva; nulo quando não concluída (I5).</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Vínculo opcional com oportunidade do mesmo tenant (Req 3).</summary>
    public OpportunityLink? OpportunityLink { get; private set; }

    /// <summary>Vínculo opcional com conta do mesmo tenant (Req 3).</summary>
    public AccountLink? AccountLink { get; private set; }

    /// <summary>Instante de criação (imutável após factory).</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Instante da última modificação.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Domain events acumulados desde a última persistência.
    /// Despacho realizado pelo <c>TransactionBehavior</c> pós-commit via Outbox (design §6.6).
    /// </summary>
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // ── Factory: Create ──────────────────────────────────────────────────────

    /// <summary>
    /// Cria uma nova atividade com os parâmetros fornecidos, aplicando os defaults
    /// (status=pending, priority=medium) e acumulando <see cref="ActivityCreated"/> (I3, Req 1).
    /// </summary>
    /// <param name="tenantId">Tenant ao qual a atividade pertence.</param>
    /// <param name="buId">Business Unit.</param>
    /// <param name="ownerId">Usuário responsável.</param>
    /// <param name="type">Tipo da atividade (lista canônica — I2).</param>
    /// <param name="title">Título não vazio (I1).</param>
    /// <param name="dueAt">Instante de vencimento.</param>
    /// <param name="now">Instante corrente (injetado via clock — sem DateTime.Now).</param>
    /// <param name="priority">Prioridade; usa medium quando nula (I3).</param>
    /// <param name="description">Descrição opcional (PII potencial, nunca logada).</param>
    /// <param name="opportunityLink">Vínculo opcional com oportunidade.</param>
    /// <param name="accountLink">Vínculo opcional com conta.</param>
    /// <param name="correlationId">Identificador de correlação para o evento.</param>
    /// <returns>Novo agregado <see cref="Activity"/> com <see cref="ActivityCreated"/> acumulado.</returns>
    /// <exception cref="TitleRequiredException">Quando <paramref name="title"/> está em branco (I1).</exception>
    public static Activity Create(
        Guid             tenantId,
        Guid             buId,
        Guid             ownerId,
        ActivityType     type,
        string           title,
        DueDate          dueAt,
        DateTimeOffset   now,
        Priority?        priority        = null,
        string?          description     = null,
        OpportunityLink? opportunityLink = null,
        AccountLink?     accountLink     = null,
        Guid?            correlationId   = null)
    {
        // I1: título não vazio
        var trimmedTitle = title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedTitle))
            throw new TitleRequiredException();

        var id       = Guid.NewGuid();
        var corrId   = correlationId ?? Guid.NewGuid();
        var activity = new Activity(
            id:              id,
            tenantId:        tenantId,
            buId:            buId,
            ownerId:         ownerId,
            type:            type,
            title:           trimmedTitle,
            dueAt:           dueAt,
            status:          ActivityStatus.Pending,  // I3
            priority:        priority ?? Priority.Default, // I3
            completedAt:     null,
            description:     description,
            opportunityLink: opportunityLink,
            accountLink:     accountLink,
            createdAt:       now,
            updatedAt:       now);

        activity._domainEvents.Add(new ActivityCreated(
            EventId:       Guid.NewGuid(),
            OccurredAt:    now,
            ActivityId:    id,
            TenantId:      tenantId,
            BuId:          buId,
            OwnerId:       ownerId,
            Type:          type.Value,
            DueAt:         dueAt.Value,
            CorrelationId: corrId,
            OpportunityId: opportunityLink?.OpportunityId,
            AccountId:     accountLink?.AccountId));

        return activity;
    }

    // ── Factory: Reconstitute ────────────────────────────────────────────────

    /// <summary>
    /// Reconstitui o agregado a partir dos dados persistidos, sem gerar domain events.
    /// Usado pelo repositório na reconstrução a partir do banco.
    /// </summary>
    public static Activity Reconstitute(
        Guid             id,
        Guid             tenantId,
        Guid             buId,
        Guid             ownerId,
        ActivityType     type,
        string           title,
        DueDate          dueAt,
        ActivityStatus   status,
        Priority         priority,
        DateTimeOffset?  completedAt,
        string?          description,
        OpportunityLink? opportunityLink,
        AccountLink?     accountLink,
        DateTimeOffset   createdAt,
        DateTimeOffset   updatedAt)
    {
        return new Activity(
            id:              id,
            tenantId:        tenantId,
            buId:            buId,
            ownerId:         ownerId,
            type:            type,
            title:           title,
            dueAt:           dueAt,
            status:          status,
            priority:        priority,
            completedAt:     completedAt,
            description:     description,
            opportunityLink: opportunityLink,
            accountLink:     accountLink,
            createdAt:       createdAt,
            updatedAt:       updatedAt);
    }

    // ── Métodos de comportamento ─────────────────────────────────────────────

    /// <summary>
    /// Altera o status da atividade conforme a máquina de estados (I4, I6).
    /// </summary>
    /// <param name="target">Status de destino.</param>
    /// <param name="now">Instante corrente.</param>
    /// <exception cref="ActivityTerminalException">Quando a atividade está em estado terminal (I6).</exception>
    /// <exception cref="InvalidStatusTransitionException">Quando a transição não é permitida (I4).</exception>
    public void ChangeStatus(ActivityStatus target, DateTimeOffset now)
    {
        GuardNotTerminal();

        if (!Status.CanTransitionTo(target))
            throw new InvalidStatusTransitionException(Status.Value, target.Value);

        Status    = target;
        UpdatedAt = now;
    }

    /// <summary>
    /// Conclui a atividade de forma idempotente (DD-004, Req 6.4, PBT-02).
    /// Se a atividade já está <c>completed</c>, é no-op sem alterar <see cref="CompletedAt"/>
    /// nem acumular novo <see cref="ActivityCompleted"/>.
    /// </summary>
    /// <param name="now">Instante corrente.</param>
    /// <param name="correlationId">Identificador de correlação para o evento.</param>
    /// <exception cref="ActivityTerminalException">Quando a atividade está <c>cancelled</c> (I6).</exception>
    public void Complete(DateTimeOffset now, Guid? correlationId = null)
    {
        // Idempotência: se já completed, no-op
        if (Status == ActivityStatus.Completed)
            return;

        // I6: rejeita se terminal (apenas cancelled, pois completed foi tratado acima)
        GuardNotTerminal();

        Status      = ActivityStatus.Completed;
        CompletedAt = now; // I5
        UpdatedAt   = now;

        _domainEvents.Add(new ActivityCompleted(
            EventId:       Guid.NewGuid(),
            OccurredAt:    now,
            ActivityId:    Id,
            TenantId:      TenantId,
            OwnerId:       OwnerId,
            CompletedAt:   now,
            CorrelationId: correlationId ?? Guid.NewGuid(),
            OpportunityId: OpportunityLink?.OpportunityId));
    }

    /// <summary>
    /// Cancela a atividade. <see cref="CompletedAt"/> permanece nulo (I5).
    /// </summary>
    /// <param name="now">Instante corrente.</param>
    /// <exception cref="ActivityTerminalException">Quando a atividade já está em estado terminal (I6).</exception>
    public void Cancel(DateTimeOffset now)
    {
        GuardNotTerminal();
        ChangeStatus(ActivityStatus.Cancelled, now);
    }

    /// <summary>
    /// Reagenda a atividade atualizando <see cref="DueAt"/> sem alterar o status (Req 8).
    /// </summary>
    /// <param name="newDueAt">Nova data de vencimento.</param>
    /// <exception cref="ActivityTerminalException">Quando a atividade está em estado terminal (I6).</exception>
    public void Reschedule(DueDate newDueAt)
    {
        GuardNotTerminal();
        DueAt     = newDueAt;
        UpdatedAt = DateTimeOffset.UtcNow; // UpdatedAt via clock — aceitável aqui pois é apenas timestamp de infra
    }

    /// <summary>
    /// Atualiza os atributos mutáveis da atividade (título, descrição, vencimento, prioridade).
    /// </summary>
    /// <param name="title">Novo título (não vazio — I1).</param>
    /// <param name="description">Nova descrição (opcional).</param>
    /// <param name="dueAt">Nova data de vencimento.</param>
    /// <param name="priority">Nova prioridade.</param>
    /// <param name="now">Instante corrente.</param>
    /// <exception cref="ActivityTerminalException">Quando a atividade está em estado terminal (I6).</exception>
    /// <exception cref="TitleRequiredException">Quando o título está em branco (I1).</exception>
    public void UpdateDetails(
        string         title,
        string?        description,
        DueDate        dueAt,
        Priority?      priority,
        DateTimeOffset now)
    {
        GuardNotTerminal();

        var trimmedTitle = title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedTitle))
            throw new TitleRequiredException();

        Title       = trimmedTitle;
        Description = description;
        DueAt       = dueAt;
        if (priority is not null)
            Priority = priority;
        UpdatedAt = now;
    }

    /// <summary>
    /// Remove os domain events acumulados após o dispatch pelo Outbox.
    /// Chamado pelo <c>TransactionBehavior</c> após persistência.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    // ── Guards privados ───────────────────────────────────────────────────────

    /// <summary>
    /// Lança <see cref="ActivityTerminalException"/> quando o status é terminal (I6).
    /// </summary>
    private void GuardNotTerminal()
    {
        if (Status.IsTerminal)
            throw new ActivityTerminalException(Status.Value);
    }
}
