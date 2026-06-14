using MediatR;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;
using PartnerManagement.Domain.Partners.ValueObjects;

namespace PartnerManagement.Application.Partners.Queries;

/// <summary>
/// Handler de <see cref="GetPartnerByIdQuery"/>.
/// Retorna o detalhe do parceiro ou lança <see cref="PartnerNotFoundException"/> (PM-ERR-007).
/// Mapeia: Req 2, Req 4, design §5.2, design §5.3.
/// </summary>
internal sealed class GetPartnerByIdHandler(IPartnerRepository partnerRepository)
    : IRequestHandler<GetPartnerByIdQuery, PartnerDetail>
{
    /// <inheritdoc/>
    public async Task<PartnerDetail> Handle(
        GetPartnerByIdQuery query,
        CancellationToken cancellationToken)
    {
        Partner? partner = await partnerRepository
            .GetByIdAsync(query.PartnerId, cancellationToken)
            .ConfigureAwait(false);

        if (partner is null)
        {
            throw new PartnerNotFoundException(query.PartnerId);
        }

        return ToDetail(partner);
    }

    private static PartnerDetail ToDetail(Partner p) =>
        new(
            PartnerId: p.Id,
            Name: p.Name.Value,
            Role: p.Role.Value,
            PctSetup: p.CommissionDefaults.PctSetup.Value,
            PctRecorrente: p.CommissionDefaults.PctRecorrente.Value,
            ContactEmail: p.Contact?.EmailAddress?.Value,
            ContactPhone: p.Contact?.PhoneNumber?.Value,
            Notes: p.Notes,
            Active: p.Status == PartnerStatus.Active,
            IsTriagePending: p.CommissionDefaults.IsTriagePending,
            CreatedAt: p.CreatedAt,
            UpdatedAt: p.UpdatedAt);
}
