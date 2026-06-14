using MediatR;
using Microsoft.Extensions.Logging;
using PartnerManagement.Application.Ports;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;

namespace PartnerManagement.Application.Partners.Commands;

/// <summary>
/// Handler do comando <see cref="ReactivatePartnerCommand"/>.
/// Invoca <c>Partner.Reactivate()</c> (idempotente); registra auditoria sempre.
/// Em transição idempotente (já ativo), não emite evento de domínio — apenas auditoria (DD-006).
/// Mapeia: Req 3, PBT-02, design §5.1, DD-006.
/// </summary>
internal sealed class ReactivatePartnerHandler(
    IPartnerRepository partnerRepository,
    IAuditPublisher auditPublisher,
    ILogger<ReactivatePartnerHandler> logger)
    : IRequestHandler<ReactivatePartnerCommand, ReactivatePartnerResult>
{
    /// <inheritdoc/>
    public async Task<ReactivatePartnerResult> Handle(
        ReactivatePartnerCommand command,
        CancellationToken cancellationToken)
    {
        Partner? partner = await partnerRepository
            .GetByIdAsync(command.PartnerId, cancellationToken)
            .ConfigureAwait(false);

        if (partner is null)
        {
            throw new PartnerNotFoundException(command.PartnerId);
        }

        bool transitionEffective = partner.Reactivate();

        string action = transitionEffective
            ? "Reactivate"
            : "ReactivateAttempt_AlreadyActive";

        // Auditoria sempre registrada (DD-006, RNF 2)
        await RecordAuditAsync(command, action, cancellationToken).ConfigureAwait(false);

        if (transitionEffective)
        {
            await partnerRepository.UpdateAsync(partner, cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Parceiro {PartnerId} reativado no tenant {TenantId}",
                command.PartnerId,
                command.TenantId);
        }
        else
        {
            logger.LogInformation(
                "Reativação idempotente: parceiro {PartnerId} já estava ativo no tenant {TenantId}",
                command.PartnerId,
                command.TenantId);
        }

        return new ReactivatePartnerResult(transitionEffective);
    }

    private Task RecordAuditAsync(
        ReactivatePartnerCommand command,
        string action,
        CancellationToken cancellationToken) =>
        auditPublisher.PublishAsync(
            entityType: "Partner",
            entityId: command.PartnerId,
            tenantId: command.TenantId,
            action: action,
            deltaJson: "{}",
            actorId: command.ActorId,
            correlationId: command.CorrelationId,
            cancellationToken: cancellationToken);
}
