using FluentValidation;
using MediatR;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Domain.Opportunities.Repositories;
using AppValidationException = OpportunityPipeline.Application.Common.ValidationException;

namespace OpportunityPipeline.Application.Opportunities.Commands;

/// <summary>
/// Command para reabrir oportunidade encerrada (won|lost → open).
/// RBAC: apenas GestorBU e TenantAdmin (OP-ERR-008).
/// Snapshot existente é preservado (PBT-11).
/// Mapeia: Req 15, RNF 4 (RBAC), design §5.1, OP-ERR-008,013.
/// </summary>
[RequiresRole(UserRole.GestorBU, UserRole.TenantAdmin)]
public sealed record ReopenOpportunityCommand : IRequest<ReopenOpportunityResult>, IAuthenticatedCommand, IMutableAuthenticatedCommand
{
    private UserRole _userRole;
    public UserRole UserRole => _userRole;
    public void SetRole(UserRole role) => _userRole = role;
    public required string CorrelationId { get; init; }

    public required Guid OpportunityId { get; init; }
    public string? Reason { get; init; }
}

/// <summary>Resultado da reabertura.</summary>
public sealed record ReopenOpportunityResult(
    Guid OpportunityId,
    bool SnapshotPreserved,
    string? PreviousCategory);

/// <summary>
/// Validator para ReopenOpportunityCommand.
/// </summary>
public sealed class ReopenOpportunityValidator : AbstractValidator<ReopenOpportunityCommand>
{
    public ReopenOpportunityValidator()
    {
        RuleFor(x => x.OpportunityId)
            .NotEmpty().WithMessage("opportunity_id é obrigatório.");
    }
}

/// <summary>
/// Handler de ReopenOpportunityCommand.
/// RBAC verificado pelo RbacBehavior antes de chegar aqui (GestorBU/TenantAdmin).
/// Verifica que snapshot existente é preservado após reabertura.
/// Mapeia: Req 15, PBT-11, design §5.3, TASK-10.
/// </summary>
public sealed class ReopenOpportunityHandler(
    IOpportunityRepository repository,
    TenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ReopenOpportunityCommand, ReopenOpportunityResult>
{
    public async Task<ReopenOpportunityResult> Handle(
        ReopenOpportunityCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var actorId = tenantContext.ActorId;
        var now = DateTimeOffset.UtcNow;

        var opportunity = await repository.GetByIdAsync(command.OpportunityId, tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new AppValidationException("opportunity_id", $"Oportunidade '{command.OpportunityId}' não encontrada.");

        // Captura estado do snapshot antes da reabertura (para verificação PBT-11)
        var snapshotBefore = opportunity.Commissions.FirstOrDefault(c => c.IsSnapshot);
        var previousCategory = opportunity.StageCategory.ToString();

        // Delega ao agregado — won|lost → open; preserva snapshot
        opportunity.Reopen(actorId, command.Reason, now);

        await repository.SaveAsync(opportunity, cancellationToken).ConfigureAwait(false);

        // Verifica que snapshot foi preservado (invariante PBT-11)
        var snapshotAfter = opportunity.Commissions.FirstOrDefault(c => c.IsSnapshot);
        bool snapshotPreserved = snapshotBefore is null
            ? snapshotAfter is null
            : snapshotAfter is not null && snapshotAfter.Id == snapshotBefore.Id;

        unitOfWork.AddDomainEvents(opportunity.DomainEvents);
        opportunity.ClearDomainEvents();
        unitOfWork.RegisterAudit(opportunity.Id, "Opportunity", new { action = "Reopen", previous_category = previousCategory });

        return new ReopenOpportunityResult(
            opportunity.Id,
            SnapshotPreserved: snapshotPreserved,
            PreviousCategory: previousCategory);
    }
}
