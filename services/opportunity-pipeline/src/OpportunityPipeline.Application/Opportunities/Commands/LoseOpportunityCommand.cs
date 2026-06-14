using FluentValidation;
using MediatR;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.Repositories;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;
using AppValidationException = OpportunityPipeline.Application.Common.ValidationException;

namespace OpportunityPipeline.Application.Opportunities.Commands;

/// <summary>
/// Command para encerrar oportunidade como perdida.
/// Motivo de perda é obrigatório (INV-7, OP-ERR-006).
/// Mapeia: Req 10, INV-7, design §5.1, OP-ERR-006,013.
/// </summary>
[RequiresRole(UserRole.Vendedor, UserRole.GestorBU, UserRole.TenantAdmin)]
public sealed record LoseOpportunityCommand : IRequest<LoseOpportunityResult>, IAuthenticatedCommand, IMutableAuthenticatedCommand
{
    private UserRole _userRole;
    public UserRole UserRole => _userRole;
    public void SetRole(UserRole role) => _userRole = role;
    public required string CorrelationId { get; init; }

    public required Guid OpportunityId { get; init; }
    public required Guid LossReasonId { get; init; }
}

/// <summary>Resultado do encerramento como perdida.</summary>
public sealed record LoseOpportunityResult(Guid OpportunityId, DateTimeOffset ClosedAt);

/// <summary>
/// Validator para LoseOpportunityCommand.
/// Mapeia: OP-ERR-006.
/// </summary>
public sealed class LoseOpportunityValidator : AbstractValidator<LoseOpportunityCommand>
{
    public LoseOpportunityValidator()
    {
        RuleFor(x => x.OpportunityId)
            .NotEmpty().WithMessage("opportunity_id é obrigatório.");

        RuleFor(x => x.LossReasonId)
            .NotEmpty().WithMessage("OP-ERR-006: loss_reason_id é obrigatório para perder uma oportunidade.");
    }
}

/// <summary>
/// Handler de LoseOpportunityCommand.
/// Mapeia: design §5.3, TASK-10.
/// </summary>
public sealed class LoseOpportunityHandler(
    IOpportunityRepository repository,
    IOrganizationReadPort organizationPort,
    TenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LoseOpportunityCommand, LoseOpportunityResult>
{
    public async Task<LoseOpportunityResult> Handle(
        LoseOpportunityCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var actorId = tenantContext.ActorId;
        var now = DateTimeOffset.UtcNow;

        var opportunity = await repository.GetByIdAsync(command.OpportunityId, tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new AppValidationException("opportunity_id", $"Oportunidade '{command.OpportunityId}' não encontrada.");

        // Resolve LossReasonRef para enriquecer o domínio
        var lossReason = await organizationPort.GetLossReasonRefAsync(tenantId, command.LossReasonId, cancellationToken).ConfigureAwait(false)
            ?? throw new AppValidationException("loss_reason_id", "OP-ERR-006: Motivo de perda não encontrado.", "OP-ERR-006");

        // Delega ao agregado — valida INV-7 e open → lost
        opportunity.Lose(lossReason, actorId, now);

        await repository.SaveAsync(opportunity, cancellationToken).ConfigureAwait(false);

        unitOfWork.AddDomainEvents(opportunity.DomainEvents);
        opportunity.ClearDomainEvents();
        unitOfWork.RegisterAudit(opportunity.Id, "Opportunity", new { action = "Lose", loss_reason_id = command.LossReasonId });

        return new LoseOpportunityResult(opportunity.Id, opportunity.ClosedAt!.Value);
    }
}
