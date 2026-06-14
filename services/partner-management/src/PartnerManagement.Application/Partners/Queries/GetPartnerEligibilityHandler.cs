using MediatR;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;
using PartnerManagement.Domain.Partners.ValueObjects;

namespace PartnerManagement.Application.Partners.Queries;

/// <summary>
/// Handler de <see cref="GetPartnerEligibilityQuery"/>.
/// Expõe o status de elegibilidade do parceiro para o opportunity-pipeline.
/// Retorna <see cref="PartnerNotFoundException"/> (PM-ERR-007) para parceiro inexistente ou fora do tenant.
/// Mapeia: Req 8, design §5.2.
/// </summary>
internal sealed class GetPartnerEligibilityHandler(IPartnerRepository partnerRepository)
    : IRequestHandler<GetPartnerEligibilityQuery, PartnerEligibilityResult>
{
    /// <inheritdoc/>
    public async Task<PartnerEligibilityResult> Handle(
        GetPartnerEligibilityQuery query,
        CancellationToken cancellationToken)
    {
        Partner? partner = await partnerRepository
            .GetByIdAsync(query.PartnerId, cancellationToken)
            .ConfigureAwait(false);

        if (partner is null)
        {
            throw new PartnerNotFoundException(query.PartnerId);
        }

        return new PartnerEligibilityResult(
            PartnerId: partner.Id,
            Active: partner.Status == PartnerStatus.Active);
    }
}
