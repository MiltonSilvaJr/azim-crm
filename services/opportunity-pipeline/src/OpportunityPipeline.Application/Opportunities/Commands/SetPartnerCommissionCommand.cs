using FluentValidation;
using MediatR;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Domain.Opportunities.Exceptions;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.Repositories;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;
using AppValidationException = OpportunityPipeline.Application.Common.ValidationException;

namespace OpportunityPipeline.Application.Opportunities.Commands;

/// <summary>
/// Command para vincular ou atualizar comissão de parceiro em uma oportunidade.
/// Rejeita se snapshot já existe (409 Conflict).
/// Rejeita mutual exclusão valor_fixo × percentuais (OP-ERR-014).
/// Pré-preenche defaults do parceiro via IPartnerReadPort.
/// Mapeia: Req 11, Req 12, design §5.1, OP-ERR-004,014,015, TASK-11.
/// </summary>
[RequiresRole(UserRole.Vendedor, UserRole.GestorBU, UserRole.TenantAdmin)]
public sealed record SetPartnerCommissionCommand : IRequest<SetPartnerCommissionResult>, IAuthenticatedCommand, IMutableAuthenticatedCommand
{
    private UserRole _userRole;
    public UserRole UserRole => _userRole;
    public void SetRole(UserRole role) => _userRole = role;
    public required string CorrelationId { get; init; }

    public required Guid OpportunityId { get; init; }
    public required Guid PartnerId { get; init; }
    public required CommissionRole Role { get; init; }

    // Percentuais (mutuamente excludentes com ValorFixoCents)
    public decimal PctSetup { get; init; }
    public decimal PctRecorrente { get; init; }
    public int MesesComissionados { get; init; }

    // Valor fixo (mutuamente excludente com percentuais)
    public long ValorFixoCents { get; init; }

    /// <summary>Se true, usa defaults do parceiro como base (preenchido pelo handler).</summary>
    public bool UsePartnerDefaults { get; init; }
}

/// <summary>Resultado da definição de comissão.</summary>
public sealed record SetPartnerCommissionResult(
    Guid OpportunityId,
    long ComissaoTotalCents,
    bool IsSnapshot);

/// <summary>
/// Validator para SetPartnerCommissionCommand.
/// Mapeia: OP-ERR-004, OP-ERR-014, OP-ERR-015.
/// </summary>
public sealed class SetPartnerCommissionValidator : AbstractValidator<SetPartnerCommissionCommand>
{
    public SetPartnerCommissionValidator()
    {
        RuleFor(x => x.OpportunityId).NotEmpty();
        RuleFor(x => x.PartnerId).NotEmpty()
            .WithMessage("OP-ERR-004: partner_id é obrigatório.");

        RuleFor(x => x)
            .Must(x => !(x.ValorFixoCents > 0 && (x.PctSetup > 0 || x.PctRecorrente > 0)))
            .WithMessage("OP-ERR-014: valor_fixo e percentuais são mutuamente excludentes.");

        RuleFor(x => x.PctSetup).InclusiveBetween(0m, 100m);
        RuleFor(x => x.PctRecorrente).InclusiveBetween(0m, 100m);
        RuleFor(x => x.MesesComissionados).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ValorFixoCents).GreaterThanOrEqualTo(0L);
    }
}

/// <summary>
/// Handler de SetPartnerCommissionCommand.
/// Rejeita se snapshot já existe. Pré-preenche defaults do parceiro.
/// Mapeia: design §5.3, TASK-11.
/// </summary>
public sealed class SetPartnerCommissionHandler(
    IOpportunityRepository repository,
    IPartnerReadPort partnerPort,
    TenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SetPartnerCommissionCommand, SetPartnerCommissionResult>
{
    public async Task<SetPartnerCommissionResult> Handle(
        SetPartnerCommissionCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var now = DateTimeOffset.UtcNow;

        var opportunity = await repository.GetByIdAsync(command.OpportunityId, tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new AppValidationException("opportunity_id", $"Oportunidade '{command.OpportunityId}' não encontrada.");

        // Rejeita se snapshot já existe (409 Conflict → SnapshotImmutableException)
        if (opportunity.Commissions.Any(c => c.IsSnapshot))
            throw new SnapshotImmutableException();

        // Valida que parceiro existe
        var partnerExists = await partnerPort.PartnerExistsAsync(tenantId, command.PartnerId, cancellationToken).ConfigureAwait(false);
        if (!partnerExists)
            throw new AppValidationException("partner_id", "OP-ERR-015: Parceiro não encontrado.", "OP-ERR-015");

        // Obtém defaults do parceiro para pré-preenchimento
        // ValorFixo na mesma moeda da oportunidade (ADR-0008)
        var commissionCurrency = opportunity.ContractValue.Currency;
        Money? valorFixo = command.ValorFixoCents > 0 ? new Money(command.ValorFixoCents, commissionCurrency) : null;
        var pctSetup = command.PctSetup;
        var pctRecorrente = command.PctRecorrente;

        if (command.UsePartnerDefaults)
        {
            var defaults = await partnerPort.GetCommissionDefaultsAsync(tenantId, command.PartnerId, cancellationToken).ConfigureAwait(false);
            if (defaults is not null)
            {
                pctSetup = pctSetup == 0m ? defaults.PctSetup : pctSetup;
                pctRecorrente = pctRecorrente == 0m ? defaults.PctRecorrente : pctRecorrente;
            }
        }

        var terms = new CommissionTerms(
            command.Role,
            pctSetup,
            pctRecorrente,
            valorFixo,
            command.MesesComissionados);

        opportunity.SetPartnerCommission(command.PartnerId, terms, now);

        await repository.SaveAsync(opportunity, cancellationToken).ConfigureAwait(false);

        unitOfWork.AddDomainEvents(opportunity.DomainEvents);
        opportunity.ClearDomainEvents();
        unitOfWork.RegisterAudit(opportunity.Id, "Opportunity", new { action = "SetPartnerCommission", partner_id = command.PartnerId });

        var commission = opportunity.Commissions.FirstOrDefault(c => !c.IsSnapshot && c.PartnerId == command.PartnerId);
        return new SetPartnerCommissionResult(
            opportunity.Id,
            commission?.Calculation.ComissaoTotal.AmountInCents ?? 0L,
            IsSnapshot: false);
    }
}
