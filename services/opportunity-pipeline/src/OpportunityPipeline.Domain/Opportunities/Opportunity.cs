using OpportunityPipeline.Domain.Opportunities.Entities;
using OpportunityPipeline.Domain.Opportunities.Events;
using OpportunityPipeline.Domain.Opportunities.Exceptions;
using OpportunityPipeline.Domain.Opportunities.Services;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities;

/// <summary>
/// Aggregate root — Oportunidade Comercial.
/// Responsável por proteger todas as invariantes INV-1..13 do ciclo de vida.
/// Este arquivo é o ponto central do domínio (design §4.1).
/// Implementação completa em TASK-06 e TASK-07.
/// </summary>
public sealed class Opportunity
{
    // =========================================================================
    // Identidade
    // =========================================================================

    /// <summary>Identificador UUID único da oportunidade.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador humano imutável (INV-5).</summary>
    public OpportunityNumber Number { get; private set; } = null!;

    /// <summary>Tenant ao qual pertence (multi-tenancy, ADR-0001).</summary>
    public Guid TenantId { get; private set; }

    // =========================================================================
    // Estado principal
    // =========================================================================

    /// <summary>Unidade de negócio.</summary>
    public Guid BuId { get; private set; }

    /// <summary>Conta à qual a oportunidade pertence.</summary>
    public Guid AccountId { get; private set; }

    /// <summary>Usuário dono (INV-1).</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>Parceiro vinculado (nullable; obrigatório se canal Parceiro — INV-4).</summary>
    public Guid? PartnerId { get; private set; }

    /// <summary>Estágio atual com categoria e metadados.</summary>
    public StageRef Stage { get; private set; } = null!;

    /// <summary>Categoria do ciclo de vida (INV-2).</summary>
    public StageCategory StageCategory { get; private set; }

    /// <summary>Canal de origem (INV-3).</summary>
    public OriginChannelRef OriginChannel { get; private set; } = null!;

    /// <summary>Título da oportunidade (sem PII — INV-13).</summary>
    public string Title { get; private set; } = null!;

    /// <summary>Valor contratual (setup, mensal, meses — INV-8).</summary>
    public ContractValue ContractValue { get; private set; } = null!;

    /// <summary>
    /// Moeda da oportunidade (ISO-4217: BRL, USD, EUR). Imutável após criação (ADR-0008).
    /// Derivada do ContractValue; persistida como coluna própria para facilitar queries.
    /// </summary>
    public string Currency => ContractValue.Currency;

    /// <summary>Probabilidade de fechamento (INV-9).</summary>
    public Probability Probability { get; private set; } = null!;

    /// <summary>Data esperada de fechamento (INV-6).</summary>
    public DateOnly? ExpectedCloseDate { get; private set; }

    /// <summary>Motivo de perda (INV-7 — obrigatório ao perder).</summary>
    public LossReasonRef? LossReason { get; private set; }

    /// <summary>Data/hora de encerramento.</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>Notas livres (sem PII — INV-13).</summary>
    public string? Notes { get; private set; }

    /// <summary>Data/hora da criação.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Data/hora da última atualização.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Usuário criador.</summary>
    public Guid CreatedBy { get; private set; }

    /// <summary>True quando marcada como estagnada (idempotente via flag).</summary>
    public bool IsStale { get; private set; }

    // =========================================================================
    // Entidades internas
    // =========================================================================

    private readonly List<OpportunityStageTransition> _transitions = [];
    private readonly List<OpportunityPartnerCommission> _commissions = [];
    private readonly List<OpportunityContactLink> _contacts = [];

    /// <summary>Linha do tempo de transições (append-only, INV, RNF 7).</summary>
    public IReadOnlyList<OpportunityStageTransition> Transitions => _transitions.AsReadOnly();

    /// <summary>Comissões de parceiro (projetada + snapshot).</summary>
    public IReadOnlyList<OpportunityPartnerCommission> Commissions => _commissions.AsReadOnly();

    /// <summary>Contatos vinculados (INV-11).</summary>
    public IReadOnlyList<OpportunityContactLink> Contacts => _contacts.AsReadOnly();

    // =========================================================================
    // Domain Events
    // =========================================================================

    private readonly List<DomainEvent> _domainEvents = [];

    /// <summary>Eventos de domínio emitidos neste ciclo (dispatched pelo handler).</summary>
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Limpa os eventos após dispatch (chamado pelo handler).</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    // Construtor privado para uso pela fábrica e reconstituição
    private Opportunity() { }

    // =========================================================================
    // Fábrica — TASK-06
    // =========================================================================

    /// <summary>
    /// Fábrica estática — cria nova oportunidade validando INV-1..5, INV-8, INV-9.
    /// Emite OpportunityCreated. Registra transição inicial.
    /// Implementado em TASK-06.
    /// </summary>
    public static Opportunity Create(
        Guid tenantId,
        Guid buId,
        Guid accountId,
        Guid ownerId,
        Guid? partnerId,
        StageRef stage,
        OriginChannelRef originChannel,
        string title,
        ContractValue contractValue,
        Probability probability,
        DateOnly? expectedCloseDate,
        string? notes,
        OpportunityNumber number,
        Guid createdBy,
        DateTimeOffset now)
    {
        // INV-1: owner_id obrigatório
        if (ownerId == Guid.Empty)
            throw new OwnerRequiredException();

        // INV-2: stage obrigatório
        ArgumentNullException.ThrowIfNull(stage);

        // INV-3: origin_channel obrigatório
        ArgumentNullException.ThrowIfNull(originChannel);

        // INV-4: canal Parceiro exige partner_id
        if (originChannel.IsPartnerChannel && (partnerId is null || partnerId == Guid.Empty))
            throw new PartnerRequiredException();

        // INV-5: number imutável (gerado externamente)
        ArgumentNullException.ThrowIfNull(number);

        // INV-9 já validada pelo construtor de Probability
        ArgumentNullException.ThrowIfNull(probability);

        // INV-8 já validada pelo construtor de ContractValue
        ArgumentNullException.ThrowIfNull(contractValue);

        var opp = new Opportunity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BuId = buId,
            AccountId = accountId,
            OwnerId = ownerId,
            PartnerId = partnerId,
            Stage = stage,
            StageCategory = stage.Category,
            OriginChannel = originChannel,
            Title = title,
            ContractValue = contractValue,
            Probability = probability,
            ExpectedCloseDate = expectedCloseDate,
            Notes = notes,
            Number = number,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now,
            IsStale = false
        };

        // Transição inicial
        opp._transitions.Add(new OpportunityStageTransition(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            opportunityId: opp.Id,
            fromStageId: null,
            toStageId: stage.StageId,
            fromCategory: null,
            toCategory: stage.Category,
            actorId: createdBy,
            occurredAt: now));

        // Emite evento de domínio
        opp._domainEvents.Add(new OpportunityCreated(
            EventId: Guid.NewGuid(),
            TenantId: tenantId,
            OpportunityId: opp.Id,
            OpportunityNumber: number.Value,
            BuId: buId,
            AccountId: accountId,
            OwnerId: ownerId,
            StageId: stage.StageId,
            OriginChannelId: originChannel.OriginChannelId,
            ActorId: createdBy,
            OccurredAt: now));

        return opp;
    }

    // =========================================================================
    // MoveStage — TASK-06
    // =========================================================================

    /// <summary>
    /// Move a oportunidade para um novo estágio.
    /// Delega a OpportunityLifecycle para validação da transição.
    /// Valida INV-6 (data de fechamento) antes de qualquer mutação.
    /// Mapeia: Req 5, INV-6, PBT-08.
    /// </summary>
    public void MoveStage(
        StageRef targetStage,
        Guid actorId,
        DateTimeOffset now,
        int propostaEnviadaOrder)
    {
        ArgumentNullException.ThrowIfNull(targetStage);

        // Valida transição antes de qualquer mutação (PBT-08)
        OpportunityLifecycle.ValidateTransition(StageCategory, targetStage.Category);

        // INV-6: data de fechamento obrigatória para estágios ≥ Proposta Enviada
        if (ExpectedCloseDatePolicy.IsRequired(targetStage.Order, propostaEnviadaOrder)
            && ExpectedCloseDate is null)
        {
            throw new ExpectedCloseDateRequiredException();
        }

        var previousStageId = Stage.StageId;
        var previousCategory = StageCategory;

        Stage = targetStage;
        StageCategory = targetStage.Category;
        Probability = new Probability(targetStage.DefaultProbability);
        UpdatedAt = now;

        _transitions.Add(new OpportunityStageTransition(
            id: Guid.NewGuid(),
            tenantId: TenantId,
            opportunityId: Id,
            fromStageId: previousStageId,
            toStageId: targetStage.StageId,
            fromCategory: previousCategory,
            toCategory: targetStage.Category,
            actorId: actorId,
            occurredAt: now));

        _domainEvents.Add(new OpportunityStageChanged(
            EventId: Guid.NewGuid(),
            TenantId: TenantId,
            OpportunityId: Id,
            FromStageId: previousStageId,
            ToStageId: targetStage.StageId,
            FromCategory: previousCategory,
            ToCategory: targetStage.Category,
            ActorId: actorId,
            OccurredAt: now));
    }

    // =========================================================================
    // Win — TASK-07
    // =========================================================================

    /// <summary>
    /// Encerra a oportunidade como ganha, congelando o snapshot de comissão.
    /// Guarda: open → won. Snapshot criado atomicamente na mesma operação.
    /// VAL-07: snapshot com comissão zero é permitido (alerta, não bloqueia).
    /// Mapeia: Req 14, INV-12, DD-002, DD-007, PBT-07.
    /// </summary>
    public void Win(Guid actorId, DateTimeOffset now)
    {
        OpportunityLifecycle.ValidateTransition(StageCategory, ValueObjects.StageCategory.Won);

        var closedAt = now;
        ClosedAt = closedAt;
        StageCategory = ValueObjects.StageCategory.Won;
        UpdatedAt = now;

        _transitions.Add(new OpportunityStageTransition(
            id: Guid.NewGuid(),
            tenantId: TenantId,
            opportunityId: Id,
            fromStageId: Stage.StageId,
            toStageId: Stage.StageId, // mesmo estágio, categoria muda
            fromCategory: ValueObjects.StageCategory.Open,
            toCategory: ValueObjects.StageCategory.Won,
            actorId: actorId,
            occurredAt: now));

        // Congela snapshot de comissão (se houver projetada)
        var projected = _commissions.FirstOrDefault(c => !c.IsSnapshot);
        if (projected is not null)
        {
            var snapshot = projected.Freeze(now);
            _commissions.Add(snapshot);

            _domainEvents.Add(new CommissionSnapshotCreated(
                EventId: Guid.NewGuid(),
                TenantId: TenantId,
                OpportunityId: Id,
                PartnerId: snapshot.PartnerId,
                CommissionId: snapshot.Id,
                ComissaoTotalCents: snapshot.Calculation.ComissaoTotal.AmountInCents,
                SnapshotAt: now,
                OccurredAt: now));
        }

        _domainEvents.Add(new OpportunityWon(
            EventId: Guid.NewGuid(),
            TenantId: TenantId,
            OpportunityId: Id,
            ValorTotalCents: ContractValue.TotalInCents,
            ClosedAt: closedAt,
            ActorId: actorId,
            OccurredAt: now));
    }

    // =========================================================================
    // Lose — TASK-07
    // =========================================================================

    /// <summary>
    /// Encerra a oportunidade como perdida. Exige motivo de perda (INV-7).
    /// Mapeia: Req 10, INV-7, OP-ERR-006.
    /// </summary>
    public void Lose(LossReasonRef lossReason, Guid actorId, DateTimeOffset now)
    {
        OpportunityLifecycle.ValidateTransition(StageCategory, ValueObjects.StageCategory.Lost);

        // INV-7
        ArgumentNullException.ThrowIfNull(lossReason, nameof(lossReason));

        LossReason = lossReason;
        ClosedAt = now;
        StageCategory = ValueObjects.StageCategory.Lost;
        UpdatedAt = now;

        _transitions.Add(new OpportunityStageTransition(
            id: Guid.NewGuid(),
            tenantId: TenantId,
            opportunityId: Id,
            fromStageId: Stage.StageId,
            toStageId: Stage.StageId,
            fromCategory: ValueObjects.StageCategory.Open,
            toCategory: ValueObjects.StageCategory.Lost,
            actorId: actorId,
            occurredAt: now));

        _domainEvents.Add(new OpportunityLost(
            EventId: Guid.NewGuid(),
            TenantId: TenantId,
            OpportunityId: Id,
            LossReasonId: lossReason.LossReasonId,
            ClosedAt: now,
            ActorId: actorId,
            OccurredAt: now));
    }

    // =========================================================================
    // Reopen — TASK-07
    // =========================================================================

    /// <summary>
    /// Reabre a oportunidade (won|lost → open). Preserva snapshot existente.
    /// RBAC verificado no handler (TenantAdmin/GestorBU).
    /// Mapeia: Req 15, PBT-11.
    /// </summary>
    public void Reopen(Guid actorId, string? reason, DateTimeOffset now)
    {
        // Reopen apenas faz sentido quando fechada (won ou lost)
        if (StageCategory == ValueObjects.StageCategory.Open)
            throw new InvalidStageTransitionException(StageCategory, ValueObjects.StageCategory.Open);

        OpportunityLifecycle.ValidateTransition(StageCategory, ValueObjects.StageCategory.Open);

        var previousCategory = StageCategory;
        StageCategory = ValueObjects.StageCategory.Open;
        ClosedAt = null;
        UpdatedAt = now;

        _transitions.Add(new OpportunityStageTransition(
            id: Guid.NewGuid(),
            tenantId: TenantId,
            opportunityId: Id,
            fromStageId: Stage.StageId,
            toStageId: Stage.StageId,
            fromCategory: previousCategory,
            toCategory: ValueObjects.StageCategory.Open,
            actorId: actorId,
            occurredAt: now));

        _domainEvents.Add(new OpportunityReopened(
            EventId: Guid.NewGuid(),
            TenantId: TenantId,
            OpportunityId: Id,
            PreviousCategory: previousCategory,
            ActorId: actorId,
            Reason: reason,
            OccurredAt: now));
    }

    // =========================================================================
    // MarkStale — TASK-07
    // =========================================================================

    /// <summary>
    /// Marca a oportunidade como estagnada. Idempotente via flag IsStale.
    /// Não emite segundo evento se já marcada.
    /// Mapeia: Req 17, RNF 9, PBT-09.
    /// </summary>
    public void MarkStale(DateTimeOffset lastActivityAt, DateTimeOffset now, DateOnly detectionPeriod)
    {
        if (IsStale)
            return; // idempotente — não emite segundo evento

        IsStale = true;
        UpdatedAt = now;

        _domainEvents.Add(new OpportunityStale(
            EventId: Guid.NewGuid(),
            TenantId: TenantId,
            OpportunityId: Id,
            BuId: BuId,
            LastActivityAt: lastActivityAt,
            DetectedAt: now,
            DetectionPeriod: detectionPeriod));
    }

    // =========================================================================
    // SetPartnerCommission — TASK-07
    // =========================================================================

    /// <summary>
    /// Define ou atualiza a comissão projetada do parceiro.
    /// Valida INV-4 (parceiro vinculado), INV-10 (no máximo 1 parceiro no MVP).
    /// Recalcula comissão via CommissionCalculator. Emite CommissionCalculated.
    /// Mapeia: Req 11, INV-4, INV-10.
    /// </summary>
    public void SetPartnerCommission(
        Guid partnerId,
        CommissionTerms terms,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(terms);

        // INV-4: parceiro deve estar vinculado
        if (PartnerId != partnerId)
            throw new PartnerRequiredException();

        var calc = CommissionCalculator.Calculate(ContractValue, terms);

        var existing = _commissions.FirstOrDefault(c => !c.IsSnapshot && c.PartnerId == partnerId);
        if (existing is not null)
        {
            existing.WithUpdatedCalculation(terms, calc);
        }
        else
        {
            // Herda a moeda da oportunidade (ADR-0008)
            _commissions.Add(new OpportunityPartnerCommission(
                id: Guid.NewGuid(),
                tenantId: TenantId,
                opportunityId: Id,
                partnerId: partnerId,
                terms: terms,
                calculation: calc,
                currency: ContractValue.Currency));
        }

        UpdatedAt = now;

        _domainEvents.Add(new CommissionCalculated(
            EventId: Guid.NewGuid(),
            TenantId: TenantId,
            OpportunityId: Id,
            PartnerId: partnerId,
            ComissaoTotalCents: calc.ComissaoTotal.AmountInCents,
            IsSnapshot: false,
            OccurredAt: now));
    }

    // =========================================================================
    // UpdateContractValue / OverrideProbability — TASK-07
    // =========================================================================

    /// <summary>
    /// Atualiza o valor contratual e recalcula forecast e comissão projetada.
    /// Mapeia: INV-8.
    /// </summary>
    public void UpdateContractValue(ContractValue newContractValue, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(newContractValue);
        ContractValue = newContractValue;
        UpdatedAt = now;

        // Recalcula comissão projetada se houver
        RecalculateProjectedCommission(now);
    }

    /// <summary>
    /// Sobrescreve a probabilidade e recalcula forecast.
    /// Mapeia: Req 7, INV-9.
    /// </summary>
    public void OverrideProbability(Probability probability, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(probability);
        Probability = probability;
        UpdatedAt = now;
    }

    private void RecalculateProjectedCommission(DateTimeOffset now)
    {
        var projected = _commissions.FirstOrDefault(c => !c.IsSnapshot);
        if (projected is null) return;

        var newCalc = CommissionCalculator.Calculate(ContractValue, projected.Terms);
        projected.WithUpdatedCalculation(projected.Terms, newCalc);

        _domainEvents.Add(new CommissionCalculated(
            EventId: Guid.NewGuid(),
            TenantId: TenantId,
            OpportunityId: Id,
            PartnerId: projected.PartnerId,
            ComissaoTotalCents: newCalc.ComissaoTotal.AmountInCents,
            IsSnapshot: false,
            OccurredAt: now));
    }

    // =========================================================================
    // LinkContact / UnlinkContact / PromotePrimary — TASK-07
    // =========================================================================

    /// <summary>
    /// Vincula um contato à oportunidade. Se for o primeiro, torna-o principal.
    /// Mapeia: Req 16, INV-11.
    /// </summary>
    public void LinkContact(Guid contactId, bool isPrimary)
    {
        if (_contacts.Any(c => c.ContactId == contactId))
            return; // já vinculado — idempotente

        // Se isPrimary e já há outro principal, remove o principal anterior
        if (isPrimary)
        {
            foreach (var c in _contacts.Where(c => c.IsPrimary))
                c.RemovePrimary();
        }

        // Se é o primeiro contato, deve ser principal
        var actualIsPrimary = isPrimary || _contacts.Count == 0;

        _contacts.Add(new OpportunityContactLink(
            id: Guid.NewGuid(),
            tenantId: TenantId,
            opportunityId: Id,
            contactId: contactId,
            isPrimary: actualIsPrimary));
    }

    /// <summary>
    /// Remove vínculo de contato. Se for o principal e houver outros, lança exceção.
    /// Mapeia: Req 16, INV-11.
    /// </summary>
    public void UnlinkContact(Guid contactId)
    {
        var link = _contacts.FirstOrDefault(c => c.ContactId == contactId);
        if (link is null) return;

        if (link.IsPrimary && _contacts.Count > 1)
            throw new PrimaryContactRequiredException();

        _contacts.Remove(link);
    }

    /// <summary>
    /// Promove um contato como principal, removendo o status do anterior.
    /// Garante exatamente 1 principal (INV-11).
    /// </summary>
    public void PromotePrimary(Guid contactId)
    {
        var link = _contacts.FirstOrDefault(c => c.ContactId == contactId)
            ?? throw new DomainException($"Contato {contactId} não está vinculado à oportunidade.");

        foreach (var c in _contacts.Where(c => c.IsPrimary))
            c.RemovePrimary();

        link.MakePrimary();
    }
}
