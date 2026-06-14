using FluentValidation;
using MediatR;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.Repositories;
using AppValidationException = OpportunityPipeline.Application.Common.ValidationException;

namespace OpportunityPipeline.Application.Opportunities.Commands;

// =========================================================================
// LinkContactCommand
// =========================================================================

/// <summary>
/// Command para vincular contato à oportunidade.
/// Valida que o contato pertence à conta da oportunidade (OP-ERR-016).
/// Garante INV-11 (exatamente 1 principal quando há contatos).
/// Mapeia: Req 16, INV-11, design §5.1, OP-ERR-009,016, TASK-11.
/// </summary>
[RequiresRole(UserRole.Vendedor, UserRole.GestorBU, UserRole.TenantAdmin)]
public sealed record LinkContactCommand : IRequest<LinkContactResult>, IAuthenticatedCommand, IMutableAuthenticatedCommand
{
    private UserRole _userRole;
    public UserRole UserRole => _userRole;
    public void SetRole(UserRole role) => _userRole = role;
    public required string CorrelationId { get; init; }

    public required Guid OpportunityId { get; init; }
    public required Guid ContactId { get; init; }
    public bool IsPrimary { get; init; }
}

/// <summary>Resultado do vínculo de contato.</summary>
public sealed record LinkContactResult(Guid OpportunityId, int TotalContacts);

/// <summary>Validator para LinkContactCommand.</summary>
public sealed class LinkContactValidator : AbstractValidator<LinkContactCommand>
{
    public LinkContactValidator()
    {
        RuleFor(x => x.OpportunityId).NotEmpty();
        RuleFor(x => x.ContactId).NotEmpty().WithMessage("OP-ERR-016: contact_id é obrigatório.");
    }
}

/// <summary>
/// Handler de LinkContactCommand.
/// Mapeia: design §5.3, TASK-11.
/// </summary>
public sealed class LinkContactHandler(
    IOpportunityRepository repository,
    IAccountReadPort accountPort,
    TenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LinkContactCommand, LinkContactResult>
{
    public async Task<LinkContactResult> Handle(
        LinkContactCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var now = DateTimeOffset.UtcNow;

        var opportunity = await repository.GetByIdAsync(command.OpportunityId, tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new AppValidationException("opportunity_id", $"Oportunidade '{command.OpportunityId}' não encontrada.");

        // Valida que contato pertence à conta da oportunidade (OP-ERR-016)
        var contactBelongs = await accountPort.ContactBelongsToAccountAsync(tenantId, opportunity.AccountId, command.ContactId, cancellationToken).ConfigureAwait(false);
        if (!contactBelongs)
            throw new AppValidationException("contact_id", "OP-ERR-016: Contato não pertence à conta da oportunidade.", "OP-ERR-016");

        opportunity.LinkContact(command.ContactId, command.IsPrimary);

        await repository.SaveAsync(opportunity, cancellationToken).ConfigureAwait(false);

        unitOfWork.AddDomainEvents(opportunity.DomainEvents);
        opportunity.ClearDomainEvents();
        unitOfWork.RegisterAudit(opportunity.Id, "Opportunity", new { action = "LinkContact", contact_id = command.ContactId });

        return new LinkContactResult(opportunity.Id, opportunity.Contacts.Count);
    }
}

// =========================================================================
// UnlinkContactCommand
// =========================================================================

/// <summary>
/// Command para remover vínculo de contato da oportunidade.
/// Não permite remover o único contato principal sem promoção alternativa.
/// Mapeia: Req 16, INV-11, OP-ERR-009, TASK-11.
/// </summary>
[RequiresRole(UserRole.Vendedor, UserRole.GestorBU, UserRole.TenantAdmin)]
public sealed record UnlinkContactCommand : IRequest<UnlinkContactResult>, IAuthenticatedCommand, IMutableAuthenticatedCommand
{
    private UserRole _userRole;
    public UserRole UserRole => _userRole;
    public void SetRole(UserRole role) => _userRole = role;
    public required string CorrelationId { get; init; }

    public required Guid OpportunityId { get; init; }
    public required Guid ContactId { get; init; }
}

/// <summary>Resultado da remoção de contato.</summary>
public sealed record UnlinkContactResult(Guid OpportunityId, int RemainingContacts);

/// <summary>Validator para UnlinkContactCommand.</summary>
public sealed class UnlinkContactValidator : AbstractValidator<UnlinkContactCommand>
{
    public UnlinkContactValidator()
    {
        RuleFor(x => x.OpportunityId).NotEmpty();
        RuleFor(x => x.ContactId).NotEmpty();
    }
}

/// <summary>
/// Handler de UnlinkContactCommand.
/// O agregado lança PrimaryContactRequiredException se o contato for o único principal.
/// Mapeia: design §5.3, TASK-11.
/// </summary>
public sealed class UnlinkContactHandler(
    IOpportunityRepository repository,
    TenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UnlinkContactCommand, UnlinkContactResult>
{
    public async Task<UnlinkContactResult> Handle(
        UnlinkContactCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;

        var opportunity = await repository.GetByIdAsync(command.OpportunityId, tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new AppValidationException("opportunity_id", $"Oportunidade '{command.OpportunityId}' não encontrada.");

        // O agregado valida INV-11 — lança PrimaryContactRequiredException se inválido
        opportunity.UnlinkContact(command.ContactId);

        await repository.SaveAsync(opportunity, cancellationToken).ConfigureAwait(false);

        unitOfWork.AddDomainEvents(opportunity.DomainEvents);
        opportunity.ClearDomainEvents();
        unitOfWork.RegisterAudit(opportunity.Id, "Opportunity", new { action = "UnlinkContact", contact_id = command.ContactId });

        return new UnlinkContactResult(opportunity.Id, opportunity.Contacts.Count);
    }
}
