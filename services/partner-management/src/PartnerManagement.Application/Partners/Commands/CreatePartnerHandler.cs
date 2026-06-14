using MediatR;
using Microsoft.Extensions.Logging;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;
using PartnerManagement.Domain.Partners.ValueObjects;

namespace PartnerManagement.Application.Partners.Commands;

/// <summary>
/// Handler do comando <see cref="CreatePartnerCommand"/>.
/// Orquestra: verifica duplicidade de nome (alerta MSG-021 não bloqueante),
/// invoca <c>Partner.Create</c> e persiste via <c>IPartnerRepository</c>.
/// Regras de negócio vivem no agregado, não aqui.
/// Mapeia: Req 1, Req 11, design §5.1, design §5.3.
/// </summary>
internal sealed class CreatePartnerHandler(
    IPartnerRepository partnerRepository,
    ICanonicalRoleProvider roleProvider,
    ILogger<CreatePartnerHandler> logger)
    : IRequestHandler<CreatePartnerCommand, CreatePartnerResult>
{
    /// <inheritdoc/>
    public async Task<CreatePartnerResult> Handle(
        CreatePartnerCommand command,
        CancellationToken cancellationToken)
    {
        // Verificar duplicidade de nome (não bloqueante — MSG-021, Req 1.7)
        IReadOnlyList<Partner> existingByName = await partnerRepository
            .FindByNameAsync(command.Name, cancellationToken)
            .ConfigureAwait(false);

        bool duplicateAlert = existingByName.Count > 0;
        if (duplicateAlert)
        {
            logger.LogInformation(
                "MSG-021: parceiro com nome semelhante já existe no tenant {TenantId}. " +
                "ConfirmCreateDespiteDuplicate={Confirm}",
                command.TenantId,
                command.ConfirmCreateDespiteDuplicate);
        }

        // Montar contato se fornecido
        PartnerContact? contact = null;
        if (command.ContactEmail is not null || command.ContactPhone is not null)
        {
            contact = PartnerContact.Create(command.ContactEmail, command.ContactPhone);
        }

        // Criar o agregado — invariantes I1..I5 protegidas no domínio
        Partner partner = Partner.Create(
            tenantId: command.TenantId,
            name: command.Name,
            role: command.Role,
            commissionDefaults: command.CommissionDefaults,
            contact: contact,
            notes: command.Notes,
            roleProvider: roleProvider,
            createdBy: command.CreatedBy);

        // Persistir (TransactionBehavior coleta os domain events via Outbox)
        await partnerRepository.AddAsync(partner, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Parceiro {PartnerId} criado no tenant {TenantId}",
            partner.Id,
            command.TenantId);

        return new CreatePartnerResult(partner.Id, duplicateAlert);
    }
}
