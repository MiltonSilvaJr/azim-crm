using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.Repositories;
using AppValidationException = OpportunityPipeline.Application.Common.ValidationException;

namespace OpportunityPipeline.Application.Opportunities.Commands;

/// <summary>
/// Opções de política de comissão no ganho.
/// Ponto de extensão VAL-07 / DD-007: commission_required_on_win.
/// Default = false (alerta, não bloqueia — decisão de produto HITL #1).
/// Mapeia: DD-007, VAL-07, TASK-10 ST-07.
/// </summary>
public sealed class WinOpportunityOptions
{
    /// <summary>
    /// Quando true, bloqueia ganho de oportunidade de canal Parceiro sem comissão.
    /// Default = false (alerta com snapshot zero permitido — VAL-07 decisão HITL #1).
    /// </summary>
    public bool CommissionRequiredOnWin { get; set; } = false;
}

/// <summary>
/// Resultado da verificação de comissão no ganho (VAL-07).
/// </summary>
public sealed record CommissionOnWinStatus(
    bool HasCommission,
    bool IsRequired,
    bool ShouldBlock,
    string? AlertMessage);

/// <summary>
/// Verificador de comissão ao ganhar — ponto de extensão VAL-07 isolado.
/// Mapeia: DD-007, VAL-07, TASK-10 ST-07.
/// </summary>
public sealed class CommissionOnWinChecker(IOptions<WinOpportunityOptions> options)
{
    public CommissionOnWinStatus Check(
        bool hasPartner,
        bool hasProjectedCommission)
    {
        if (!hasPartner)
            return new CommissionOnWinStatus(true, false, false, null);

        if (hasProjectedCommission)
            return new CommissionOnWinStatus(true, false, false, null);

        // Canal Parceiro sem comissão
        var isRequired = options.Value.CommissionRequiredOnWin;
        var shouldBlock = isRequired;
        var alert = isRequired
            ? "OP-ERR-017: Comissão é obrigatória para ganhar oportunidade de canal Parceiro."
            : "AVISO (VAL-07): Oportunidade ganha sem comissão de parceiro — snapshot com valor zero será registrado.";

        return new CommissionOnWinStatus(false, isRequired, shouldBlock, alert);
    }
}

/// <summary>
/// Command para encerrar oportunidade como ganha, com snapshot imutável de comissão.
/// Handler mais sensível: transação única — agregado + snapshot + transição + outbox + auditoria.
/// Falha em qualquer passo → rollback total (Req 14.4, RNF 5.4).
/// Mapeia: Req 14, INV-12, DD-002, DD-007/VAL-07, PBT-07, design §5.1, OP-ERR-013,017.
/// </summary>
[RequiresRole(UserRole.Vendedor, UserRole.GestorBU, UserRole.TenantAdmin)]
public sealed record WinOpportunityCommand : IRequest<WinOpportunityResult>, IAuthenticatedCommand, IIdempotentCommand, IMutableAuthenticatedCommand
{
    private UserRole _userRole;
    public UserRole UserRole => _userRole;
    public void SetRole(UserRole role) => _userRole = role;
    public required string CorrelationId { get; init; }
    public string? IdempotencyKey { get; init; }

    public required Guid OpportunityId { get; init; }
}

/// <summary>Resultado do ganho de oportunidade.</summary>
public sealed record WinOpportunityResult(
    Guid OpportunityId,
    DateTimeOffset ClosedAt,
    bool CommissionSnapshotCreated,
    string? CommissionAlert);

/// <summary>
/// Validator para WinOpportunityCommand.
/// </summary>
public sealed class WinOpportunityValidator : AbstractValidator<WinOpportunityCommand>
{
    public WinOpportunityValidator()
    {
        RuleFor(x => x.OpportunityId)
            .NotEmpty().WithMessage("opportunity_id é obrigatório.");
    }
}

/// <summary>
/// Handler de WinOpportunityCommand — o mais sensível da Onda 3.
/// Atomicidade garantida pelo TransactionBehavior (abre e fecha a transação).
/// Handler APENAS orquestra: carrega → valida VAL-07 → Win() → persiste → Outbox + audit.
/// Mapeia: Req 14, RNF 5.4, DD-002, DD-007, PBT-07, design §5.1 §5.3, TASK-10.
/// </summary>
public sealed class WinOpportunityHandler(
    IOpportunityRepository repository,
    CommissionOnWinChecker commissionChecker,
    TenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<WinOpportunityCommand, WinOpportunityResult>
{
    public async Task<WinOpportunityResult> Handle(
        WinOpportunityCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var actorId = tenantContext.ActorId;
        var now = DateTimeOffset.UtcNow;

        // Carrega agregado completo (com comissões e transições)
        var opportunity = await repository.GetByIdAsync(command.OpportunityId, tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new AppValidationException("opportunity_id", $"Oportunidade '{command.OpportunityId}' não encontrada.");

        // VAL-07 / DD-007: verifica comissão antes de ganhar
        bool hasPartner = opportunity.PartnerId.HasValue;
        bool hasProjectedCommission = opportunity.Commissions.Any(c => !c.IsSnapshot);
        var commissionStatus = commissionChecker.Check(hasPartner, hasProjectedCommission);

        if (commissionStatus.ShouldBlock)
            throw new AppValidationException("commission", commissionStatus.AlertMessage!, "OP-ERR-017");

        // Delega ao agregado — open → won, Freeze snapshot, transição, closed_at, eventos
        opportunity.Win(actorId, now);

        // Persiste agregado (inclui snapshot criado dentro de Win())
        await repository.SaveAsync(opportunity, cancellationToken).ConfigureAwait(false);

        // Acumula eventos (OpportunityWon + CommissionSnapshotCreated se havia comissão)
        unitOfWork.AddDomainEvents(opportunity.DomainEvents);
        opportunity.ClearDomainEvents();

        // Auditoria imutável (mesma transação)
        unitOfWork.RegisterAudit(opportunity.Id, "Opportunity", new
        {
            action = "Win",
            commission_snapshot_created = hasProjectedCommission
        });

        return new WinOpportunityResult(
            OpportunityId: opportunity.Id,
            ClosedAt: opportunity.ClosedAt!.Value,
            CommissionSnapshotCreated: hasProjectedCommission,
            CommissionAlert: commissionStatus.AlertMessage);
    }
}
