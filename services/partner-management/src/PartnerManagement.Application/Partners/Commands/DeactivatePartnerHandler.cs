using MediatR;
using Microsoft.Extensions.Logging;
using PartnerManagement.Application.Ports;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;

namespace PartnerManagement.Application.Partners.Commands;

/// <summary>
/// Handler do comando <see cref="DeactivatePartnerCommand"/>.
/// Invoca <c>Partner.Deactivate()</c> (idempotente); registra auditoria sempre.
/// Em transição idempotente (já inativo), não emite evento de domínio — apenas auditoria (DD-006).
/// Mapeia: Req 3, PBT-02, design §5.1, DD-006.
/// </summary>
internal sealed class DeactivatePartnerHandler(
    IPartnerRepository partnerRepository,
    IAuditPublisher auditPublisher,
    ILogger<DeactivatePartnerHandler> logger)
    : IRequestHandler<DeactivatePartnerCommand, DeactivatePartnerResult>
{
    /// <inheritdoc/>
    public async Task<DeactivatePartnerResult> Handle(
        DeactivatePartnerCommand command,
        CancellationToken cancellationToken)
    {
        Partner? partner = await partnerRepository
            .GetByIdAsync(command.PartnerId, cancellationToken)
            .ConfigureAwait(false);

        if (partner is null)
        {
            throw new PartnerNotFoundException(command.PartnerId);
        }

        // Idempotente: retorna false se já estava inativo (sem evento de transição)
        bool transitionEffective = partner.Deactivate();

        string action = transitionEffective
            ? "Deactivate"
            : "DeactivateAttempt_AlreadyInactive";

        // Auditoria sempre registrada (DD-006, RNF 2)
        await RecordAuditAsync(command, action, cancellationToken).ConfigureAwait(false);

        if (transitionEffective)
        {
            await partnerRepository.UpdateAsync(partner, cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Parceiro {PartnerId} inativado no tenant {TenantId}",
                command.PartnerId,
                command.TenantId);
        }
        else
        {
            logger.LogInformation(
                "Inativação idempotente: parceiro {PartnerId} já estava inativo no tenant {TenantId}",
                command.PartnerId,
                command.TenantId);
        }

        return new DeactivatePartnerResult(transitionEffective);
    }

    private Task RecordAuditAsync(
        DeactivatePartnerCommand command,
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
