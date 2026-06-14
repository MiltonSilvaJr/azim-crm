using FluentValidation;
using MediatR;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Domain.Opportunities;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.Repositories;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Application.Opportunities.Commands;

/// <summary>
/// Command para criar uma nova oportunidade comercial.
/// Handler orquestra: geração de número → validação referências → Create → persistência → eventos.
/// Mapeia: Req 1, Req 2, Req 3, Req 4, Req 7, Req 8, design §5.1, OP-ERR-001..004,010,011.
/// </summary>
[RequiresRole(UserRole.Vendedor, UserRole.GestorBU, UserRole.TenantAdmin)]
public sealed record CreateOpportunityCommand : IRequest<CreateOpportunityResult>, IAuthenticatedCommand, IIdempotentCommand, IMutableAuthenticatedCommand
{
    // Contexto de autenticação (injetado pelo TenantBehavior)
    private UserRole _userRole;
    public UserRole UserRole => _userRole;
    public void SetRole(UserRole role) => _userRole = role;
    public required string CorrelationId { get; init; }
    public string? IdempotencyKey { get; init; }

    // Dados da oportunidade
    public required Guid AccountId { get; init; }
    public required Guid BuId { get; init; }
    public required Guid StageId { get; init; }
    public required Guid OriginChannelId { get; init; }
    public required Guid OwnerId { get; init; }
    public required string Title { get; init; }

    // Valores do contrato (centavos)
    public long ValorSetupCents { get; init; }
    public long ValorMensalCents { get; init; }
    public int DuracaoMeses { get; init; }

    // Probabilidade (null = usar default do estágio)
    public int? Probabilidade { get; init; }

    public DateOnly? ExpectedCloseDate { get; init; }
    public string? Notes { get; init; }

    // Canal Parceiro
    public Guid? PartnerId { get; init; }
}

/// <summary>
/// Resultado da criação de oportunidade.
/// </summary>
public sealed record CreateOpportunityResult(
    Guid Id,
    string OpportunityNumber,
    Guid TenantId);

/// <summary>
/// Validator FluentValidation para CreateOpportunityCommand.
/// Valida campos obrigatórios e intervalos (OP-ERR-001..004, OP-ERR-007, OP-ERR-012).
/// Mapeia: design §5.5.
/// </summary>
public sealed class CreateOpportunityValidator : AbstractValidator<CreateOpportunityCommand>
{
    public CreateOpportunityValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("OP-ERR-001: account_id é obrigatório.");

        RuleFor(x => x.BuId)
            .NotEmpty().WithMessage("OP-ERR-001: bu_id é obrigatório.");

        RuleFor(x => x.StageId)
            .NotEmpty().WithMessage("OP-ERR-001: stage_id é obrigatório.");

        RuleFor(x => x.OriginChannelId)
            .NotEmpty().WithMessage("OP-ERR-003: origin_channel_id é obrigatório.");

        RuleFor(x => x.OwnerId)
            .NotEmpty().WithMessage("OP-ERR-002: owner_id é obrigatório.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("OP-ERR-001: title é obrigatório.")
            .MaximumLength(500);

        RuleFor(x => x.ValorSetupCents)
            .GreaterThanOrEqualTo(0).WithMessage("valor_setup não pode ser negativo.");

        RuleFor(x => x.ValorMensalCents)
            .GreaterThanOrEqualTo(0).WithMessage("valor_mensal não pode ser negativo.");

        RuleFor(x => x.DuracaoMeses)
            .GreaterThanOrEqualTo(0).WithMessage("duracao_meses não pode ser negativo.")
            .Must((cmd, meses) => cmd.ValorMensalCents == 0 || meses > 0)
            .WithMessage("OP-ERR-012: duracao_meses é obrigatório quando há valor mensal.");

        RuleFor(x => x.Probabilidade)
            .InclusiveBetween(0, 100)
            .When(x => x.Probabilidade.HasValue)
            .WithMessage("OP-ERR-007: probabilidade deve estar entre 0 e 100.");
    }
}

/// <summary>
/// Handler de CreateOpportunityCommand.
/// Orquestra: resolver número → validar semântica → criar agregado → persistir.
/// Mapeia: design §5.3, TASK-09.
/// </summary>
public sealed class CreateOpportunityHandler(
    IOpportunityRepository repository,
    IOpportunityNumberGenerator numberGenerator,
    IOrganizationReadPort organizationPort,
    IAccountReadPort accountPort,
    IPartnerReadPort partnerPort,
    TenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateOpportunityCommand, CreateOpportunityResult>
{
    public async Task<CreateOpportunityResult> Handle(
        CreateOpportunityCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var actorId = tenantContext.ActorId;
        var now = DateTimeOffset.UtcNow;

        // Validação semântica: conta existe
        var accountExists = await accountPort.AccountExistsAsync(tenantId, command.AccountId, cancellationToken).ConfigureAwait(false);
        if (!accountExists)
            throw new Common.ValidationException("account_id", "OP-ERR-010: Conta não encontrada.", "OP-ERR-010");

        // Validação semântica: owner com membership na BU
        var ownerValid = await organizationPort.ValidateOwnerMembershipAsync(tenantId, command.BuId, command.OwnerId, cancellationToken).ConfigureAwait(false);
        if (!ownerValid)
            throw new Common.ValidationException("owner_id", "OP-ERR-002: owner_id não é usuário ativo na BU.", "OP-ERR-002");

        // Resolve StageRef
        var stage = await organizationPort.GetStageRefAsync(tenantId, command.StageId, cancellationToken).ConfigureAwait(false)
            ?? throw new Common.ValidationException("stage_id", "OP-ERR-001: Estágio não encontrado.", "OP-ERR-001");

        // Resolve OriginChannelRef
        var originChannel = await organizationPort.GetOriginChannelRefAsync(tenantId, command.OriginChannelId, cancellationToken).ConfigureAwait(false)
            ?? throw new Common.ValidationException("origin_channel_id", "OP-ERR-003: Canal de origem não encontrado.", "OP-ERR-003");

        // INV-4: canal Parceiro exige parceiro
        if (originChannel.IsPartnerChannel)
        {
            if (!command.PartnerId.HasValue || command.PartnerId == Guid.Empty)
                throw new Common.ValidationException("partner_id", "OP-ERR-004: Parceiro é obrigatório para canal Parceiro.", "OP-ERR-004");

            var partnerExists = await partnerPort.PartnerExistsAsync(tenantId, command.PartnerId.Value, cancellationToken).ConfigureAwait(false);
            if (!partnerExists)
                throw new Common.ValidationException("partner_id", "OP-ERR-015: Parceiro não encontrado.", "OP-ERR-015");
        }

        // Gera número atômico
        var number = await numberGenerator.NextAsync(tenantId, cancellationToken).ConfigureAwait(false);

        // Determina probabilidade
        var probabilityValue = command.Probabilidade ?? stage.DefaultProbability;
        var probability = new Probability(probabilityValue);

        // ContractValue
        var contractValue = new ContractValue(
            new Money(command.ValorSetupCents),
            new Money(command.ValorMensalCents),
            command.DuracaoMeses);

        // Cria agregado
        var opportunity = Opportunity.Create(
            tenantId: tenantId,
            buId: command.BuId,
            accountId: command.AccountId,
            ownerId: command.OwnerId,
            partnerId: command.PartnerId,
            stage: stage,
            originChannel: originChannel,
            title: command.Title,
            contractValue: contractValue,
            probability: probability,
            expectedCloseDate: command.ExpectedCloseDate,
            notes: command.Notes,
            number: number,
            createdBy: actorId,
            now: now);

        // Persiste
        await repository.AddAsync(opportunity, cancellationToken).ConfigureAwait(false);

        // Acumula eventos para dispatch pelo TransactionBehavior (via Outbox)
        unitOfWork.AddDomainEvents(opportunity.DomainEvents);
        opportunity.ClearDomainEvents();

        // Registra auditoria no UoW (mesma transação)
        unitOfWork.RegisterAudit(opportunity.Id, "Opportunity", new { action = "Create", opportunity_number = number.Value });

        return new CreateOpportunityResult(opportunity.Id, number.Value, tenantId);
    }
}
