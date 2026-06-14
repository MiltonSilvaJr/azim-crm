using MediatR;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;
using PartnerManagement.Domain.Partners.ValueObjects;

namespace PartnerManagement.Application.Partners.Queries;

/// <summary>
/// Handler de <see cref="ListPartnersQuery"/>.
/// Retorna página de parceiros aplicando filtros de status e triagem.
/// Mapeia: Req 4, Req 11.3, RNF 7.1, design §5.2, design §5.3.
/// </summary>
internal sealed class ListPartnersHandler(IPartnerRepository partnerRepository)
    : IRequestHandler<ListPartnersQuery, ListPartnersResult>
{
    /// <inheritdoc/>
    public async Task<ListPartnersResult> Handle(
        ListPartnersQuery query,
        CancellationToken cancellationToken)
    {
        (IReadOnlyList<Partner> partners, int totalCount) = await partnerRepository
            .ListAsync(query.Active, query.TriagePending, query.Page, query.PageSize, cancellationToken)
            .ConfigureAwait(false);

        List<PartnerSummary> summaries = partners.Select(ToSummary).ToList();

        return new ListPartnersResult(summaries, totalCount, query.Page, query.PageSize);
    }

    private static PartnerSummary ToSummary(Partner p) =>
        new(
            PartnerId: p.Id,
            Name: p.Name.Value,
            Role: p.Role.Value,
            PctSetup: p.CommissionDefaults.PctSetup.Value,
            PctRecorrente: p.CommissionDefaults.PctRecorrente.Value,
            Active: p.Status == PartnerStatus.Active,
            IsTriagePending: p.CommissionDefaults.IsTriagePending);
}
