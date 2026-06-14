using MediatR;
using Microsoft.Extensions.Logging;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;
using PartnerManagement.Domain.Partners.ValueObjects;

namespace PartnerManagement.Application.Partners.Commands;

/// <summary>
/// Handler do comando <see cref="UpdatePartnerCommand"/>.
/// Orquestra: carrega o agregado, invoca <c>UpdateProfile</c> e <c>UpdateContact</c> e persiste.
/// Retorna PM-ERR-007 via exceção quando o parceiro não existe ou está fora do tenant.
/// Mapeia: Req 2, Req 5, Req 6, Req 7, design §5.1, design §5.3.
/// </summary>
internal sealed class UpdatePartnerHandler(
    IPartnerRepository partnerRepository,
    ICanonicalRoleProvider roleProvider,
    ILogger<UpdatePartnerHandler> logger)
    : IRequestHandler<UpdatePartnerCommand, UpdatePartnerResult>
{
    /// <inheritdoc/>
    public async Task<UpdatePartnerResult> Handle(
        UpdatePartnerCommand command,
        CancellationToken cancellationToken)
    {
        Partner? partner = await partnerRepository
            .GetByIdAsync(command.PartnerId, cancellationToken)
            .ConfigureAwait(false);

        if (partner is null)
        {
            throw new PartnerNotFoundException(command.PartnerId);
        }

        // Montar contato
        PartnerContact? contact = null;
        if (command.ContactEmail is not null || command.ContactPhone is not null)
        {
            contact = PartnerContact.Create(command.ContactEmail, command.ContactPhone);
        }

        // Delega invariantes ao domínio
        partner.UpdateProfile(
            name: command.Name,
            role: command.Role,
            commissionDefaults: command.CommissionDefaults,
            notes: command.Notes,
            roleProvider: roleProvider,
            updatedBy: command.UpdatedBy);

        partner.UpdateContact(contact);

        await partnerRepository.UpdateAsync(partner, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Parceiro {PartnerId} atualizado no tenant {TenantId}",
            command.PartnerId,
            command.TenantId);

        return new UpdatePartnerResult();
    }
}
