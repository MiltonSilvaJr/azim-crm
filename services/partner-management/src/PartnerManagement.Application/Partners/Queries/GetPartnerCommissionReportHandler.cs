using MediatR;
using Microsoft.Extensions.Logging;
using PartnerManagement.Application.Ports;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;

namespace PartnerManagement.Application.Partners.Queries;

/// <summary>
/// Handler de <see cref="GetPartnerCommissionReportQuery"/>.
/// Compõe dados do cadastro com o read model do pipeline, linha a linha por oportunidade.
/// Não implementa fórmula de comissão (DD-003).
/// Em indisponibilidade do read port, aplica degradação parcial.
/// Mapeia: Req 10, design §5.2, design §5.3.
/// </summary>
internal sealed class GetPartnerCommissionReportHandler(
    IPartnerRepository partnerRepository,
    IPartnerCommissionReadPort commissionReadPort,
    ILogger<GetPartnerCommissionReportHandler> logger)
    : IRequestHandler<GetPartnerCommissionReportQuery, CommissionReportResult>
{
    /// <inheritdoc/>
    public async Task<CommissionReportResult> Handle(
        GetPartnerCommissionReportQuery query,
        CancellationToken cancellationToken)
    {
        // PM-ERR-011 — período inválido
        if (query.From >= query.To)
        {
            throw new InvalidCommissionPeriodException(query.From, query.To);
        }

        // PM-ERR-007 — parceiro não encontrado
        Partner? partner = await partnerRepository
            .GetByIdAsync(query.PartnerId, cancellationToken)
            .ConfigureAwait(false);

        if (partner is null)
        {
            throw new PartnerNotFoundException(query.PartnerId);
        }

        IReadOnlyList<CommissionLine>? lines = null;
        bool commissionUnavailable = false;

        try
        {
            lines = await commissionReadPort.GetCommissionLinesAsync(
                query.TenantId,
                query.PartnerId,
                query.From,
                query.To,
                query.CorrelationId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Read port indisponível para report do partner {PartnerId}. Degradação parcial aplicada.",
                query.PartnerId);
            commissionUnavailable = true;
        }

        if (commissionUnavailable || lines is null)
        {
            return new CommissionReportResult(
                PartnerId: query.PartnerId,
                PeriodFrom: query.From,
                PeriodTo: query.To,
                Lines: Array.Empty<CommissionReportLine>(),
                CommissionUnavailable: true);
        }

        IReadOnlyList<CommissionReportLine> reportLines = lines
            .Select(l => new CommissionReportLine(l.OpportunityId, l.CommissionCents, l.IsSnapshot, l.OccurredAt))
            .ToList();

        return new CommissionReportResult(
            PartnerId: query.PartnerId,
            PeriodFrom: query.From,
            PeriodTo: query.To,
            Lines: reportLines,
            CommissionUnavailable: false);
    }
}
