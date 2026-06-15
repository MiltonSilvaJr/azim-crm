using FluentValidation;
using MediatR;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.Repositories;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Application.Opportunities.Commands;

/// <summary>
/// Command para editar campos permitidos de uma oportunidade existente.
/// Campos editáveis: título, notas, valor contratual, probabilidade, owner, data de fechamento.
/// Mapeia: Req 1 (edição), Req 7 (probabilidade), Req 8 (TCV), design §5.1, OP-ERR-002,007,012.
/// </summary>
[RequiresRole(UserRole.Vendedor, UserRole.GestorBU, UserRole.TenantAdmin)]
public sealed record UpdateOpportunityCommand : IRequest<UpdateOpportunityResult>, IAuthenticatedCommand, IMutableAuthenticatedCommand
{
    private UserRole _userRole;
    public UserRole UserRole => _userRole;
    public void SetRole(UserRole role) => _userRole = role;
    public required string CorrelationId { get; init; }

    public required Guid OpportunityId { get; init; }

    // Campos atualizáveis (null = não alterar)
    public string? Title { get; init; }
    public string? Notes { get; init; }
    public long? ValorSetupCents { get; init; }
    public long? ValorMensalCents { get; init; }
    public int? DuracaoMeses { get; init; }
    public int? Probabilidade { get; init; }
    public Guid? OwnerId { get; init; }
    public DateOnly? ExpectedCloseDate { get; init; }
}

/// <summary>Resultado da atualização.</summary>
public sealed record UpdateOpportunityResult(Guid Id, DateTimeOffset UpdatedAt);

/// <summary>
/// Validator para UpdateOpportunityCommand.
/// Mapeia: OP-ERR-007, OP-ERR-012.
/// </summary>
public sealed class UpdateOpportunityValidator : AbstractValidator<UpdateOpportunityCommand>
{
    public UpdateOpportunityValidator()
    {
        RuleFor(x => x.OpportunityId)
            .NotEmpty().WithMessage("opportunity_id é obrigatório.");

        RuleFor(x => x.Probabilidade)
            .InclusiveBetween(0, 100)
            .When(x => x.Probabilidade.HasValue)
            .WithMessage("OP-ERR-007: probabilidade deve estar entre 0 e 100.");

        RuleFor(x => x.ValorSetupCents)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ValorSetupCents.HasValue)
            .WithMessage("valor_setup não pode ser negativo.");

        RuleFor(x => x.ValorMensalCents)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ValorMensalCents.HasValue)
            .WithMessage("valor_mensal não pode ser negativo.");

        // OP-ERR-012: duracao_meses obrigatório quando há valor mensal
        RuleFor(x => x.DuracaoMeses)
            .Must((cmd, meses) => !cmd.ValorMensalCents.HasValue || cmd.ValorMensalCents == 0 || (meses.HasValue && meses > 0))
            .WithMessage("OP-ERR-012: duracao_meses é obrigatório quando há valor mensal.");
    }
}

/// <summary>
/// Handler de UpdateOpportunityCommand.
/// Mapeia: design §5.3, TASK-09.
/// </summary>
public sealed class UpdateOpportunityHandler(
    IOpportunityRepository repository,
    IOrganizationReadPort organizationPort,
    TenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateOpportunityCommand, UpdateOpportunityResult>
{
    public async Task<UpdateOpportunityResult> Handle(
        UpdateOpportunityCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var now = DateTimeOffset.UtcNow;

        var opportunity = await repository.GetByIdAsync(command.OpportunityId, tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new Common.ValidationException("opportunity_id", $"Oportunidade '{command.OpportunityId}' não encontrada.");

        // Valida novo owner se informado
        if (command.OwnerId.HasValue)
        {
            var ownerValid = await organizationPort.ValidateOwnerMembershipAsync(tenantId, opportunity.BuId, command.OwnerId.Value, cancellationToken).ConfigureAwait(false);
            if (!ownerValid)
                throw new Common.ValidationException("owner_id", "OP-ERR-002: owner_id não é usuário ativo na BU.", "OP-ERR-002");
        }

        // Atualiza ContractValue se algum dos campos foi informado
        bool updateContract = command.ValorSetupCents.HasValue || command.ValorMensalCents.HasValue || command.DuracaoMeses.HasValue;
        if (updateContract)
        {
            // Moeda imutável — herda sempre do ContractValue existente (ADR-0008)
            var currency = opportunity.ContractValue.Currency;
            var newSetup = command.ValorSetupCents.HasValue ? new Money(command.ValorSetupCents.Value, currency) : opportunity.ContractValue.Setup;
            var newMensal = command.ValorMensalCents.HasValue ? new Money(command.ValorMensalCents.Value, currency) : opportunity.ContractValue.Mensal;
            var newMeses = command.DuracaoMeses ?? opportunity.ContractValue.DuracaoMeses;

            opportunity.UpdateContractValue(new ContractValue(newSetup, newMensal, newMeses), now);
        }

        // Atualiza probabilidade
        if (command.Probabilidade.HasValue)
            opportunity.OverrideProbability(new Probability(command.Probabilidade.Value), now);

        await repository.SaveAsync(opportunity, cancellationToken).ConfigureAwait(false);

        unitOfWork.AddDomainEvents(opportunity.DomainEvents);
        opportunity.ClearDomainEvents();
        unitOfWork.RegisterAudit(opportunity.Id, "Opportunity", new { action = "Update" });

        return new UpdateOpportunityResult(opportunity.Id, now);
    }
}
