using FluentValidation;
using MediatR;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.Repositories;

namespace OpportunityPipeline.Application.Opportunities.Commands;

/// <summary>
/// Command para mover a oportunidade para um novo estágio (drag-and-drop).
/// Valida transição via OpportunityLifecycle e INV-6 (data de fechamento).
/// Mapeia: Req 5, INV-6, design §5.1, OP-ERR-005,013.
/// </summary>
[RequiresRole(UserRole.Vendedor, UserRole.GestorBU, UserRole.TenantAdmin)]
public sealed record MoveStageCommand : IRequest<MoveStageResult>, IAuthenticatedCommand, IMutableAuthenticatedCommand
{
    private UserRole _userRole;
    public UserRole UserRole => _userRole;
    public void SetRole(UserRole role) => _userRole = role;
    public required string CorrelationId { get; init; }

    public required Guid OpportunityId { get; init; }
    public required Guid TargetStageId { get; init; }
}

/// <summary>Resultado da movimentação de estágio.</summary>
public sealed record MoveStageResult(Guid OpportunityId, string ToStageCategory);

/// <summary>
/// Validator para MoveStageCommand.
/// Mapeia: OP-ERR-013, OP-ERR-005.
/// </summary>
public sealed class MoveStageValidator : AbstractValidator<MoveStageCommand>
{
    public MoveStageValidator()
    {
        RuleFor(x => x.OpportunityId)
            .NotEmpty().WithMessage("opportunity_id é obrigatório.");

        RuleFor(x => x.TargetStageId)
            .NotEmpty().WithMessage("target_stage_id é obrigatório.");
    }
}

/// <summary>
/// Handler de MoveStageCommand.
/// Mapeia: design §5.3, TASK-09.
/// </summary>
public sealed class MoveStageHandler(
    IOpportunityRepository repository,
    IOrganizationReadPort organizationPort,
    TenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<MoveStageCommand, MoveStageResult>
{
    public async Task<MoveStageResult> Handle(
        MoveStageCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var actorId = tenantContext.ActorId;
        var now = DateTimeOffset.UtcNow;

        var opportunity = await repository.GetByIdAsync(command.OpportunityId, tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new Common.ValidationException("opportunity_id", $"Oportunidade '{command.OpportunityId}' não encontrada.");

        // Resolve StageRef do estágio destino
        var targetStage = await organizationPort.GetStageRefAsync(tenantId, command.TargetStageId, cancellationToken).ConfigureAwait(false)
            ?? throw new Common.ValidationException("target_stage_id", "OP-ERR-001: Estágio de destino não encontrado.", "OP-ERR-001");

        // Obtém order de "Proposta Enviada" para INV-6
        var propostaEnviadaOrder = await organizationPort.GetPropostaEnviadaOrderAsync(tenantId, opportunity.BuId, cancellationToken).ConfigureAwait(false);

        // Delega ao agregado — lança InvalidStageTransitionException ou ExpectedCloseDateRequiredException
        opportunity.MoveStage(targetStage, actorId, now, propostaEnviadaOrder);

        await repository.SaveAsync(opportunity, cancellationToken).ConfigureAwait(false);

        unitOfWork.AddDomainEvents(opportunity.DomainEvents);
        opportunity.ClearDomainEvents();
        unitOfWork.RegisterAudit(opportunity.Id, "Opportunity", new { action = "MoveStage", to_stage_id = command.TargetStageId });

        return new MoveStageResult(opportunity.Id, targetStage.Category.ToString());
    }
}
