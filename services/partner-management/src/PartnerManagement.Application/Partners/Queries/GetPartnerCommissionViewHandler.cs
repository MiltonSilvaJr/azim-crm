using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using PartnerManagement.Application.Ports;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;

namespace PartnerManagement.Application.Partners.Queries;

/// <summary>
/// Handler de <see cref="GetPartnerCommissionViewQuery"/>.
/// Compõe dados do cadastro com o read model do pipeline via <see cref="IPartnerCommissionReadPort"/>.
/// Em indisponibilidade do read port, aplica degradação parcial: retorna dados de cadastro
/// com <c>CommissionUnavailable = true</c> (design §5.3, §15).
/// Não implementa fórmula de comissão (DD-003): apenas agrega linhas do read model.
/// Registra span OpenTelemetry e duração da consulta via <see cref="IPartnerMetrics"/> (design §11).
/// Mapeia: Req 9, PBT-01, PBT-05, design §5.2, TASK-26.
/// </summary>
internal sealed class GetPartnerCommissionViewHandler(
    IPartnerRepository partnerRepository,
    IPartnerCommissionReadPort commissionReadPort,
    IPartnerMetrics metrics,
    ILogger<GetPartnerCommissionViewHandler> logger)
    : IRequestHandler<GetPartnerCommissionViewQuery, CommissionViewResult>
{
    /// <summary>ActivitySource para spans OpenTelemetry (design §11).</summary>
    private static readonly ActivitySource ActivitySource = new("PartnerManagement.GetPartnerCommissionView");

    /// <inheritdoc/>
    public async Task<CommissionViewResult> Handle(
        GetPartnerCommissionViewQuery query,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        using Activity? activity = ActivitySource.StartActivity(
            "GetPartnerCommissionView",
            ActivityKind.Internal);
        activity?.SetTag("partner_id", query.PartnerId.ToString());
        activity?.SetTag("tenant_id", query.TenantId.ToString());
        activity?.SetTag("correlation_id", query.CorrelationId);

        try
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

            // Tentar ler do read port (degradação parcial em falha)
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
                    "Read port indisponível para partner {PartnerId}. Degradação parcial aplicada.",
                    query.PartnerId);
                commissionUnavailable = true;
                activity?.SetTag("commission_unavailable", true);
            }

            if (commissionUnavailable || lines is null)
            {
                return new CommissionViewResult(
                    PartnerId: query.PartnerId,
                    PeriodFrom: query.From,
                    PeriodTo: query.To,
                    ProjectedCommissionCents: 0L,
                    ConsolidatedCommissionCents: 0L,
                    CommissionUnavailable: true);
            }

            // Agregação das linhas: soma projetada (abertas) + soma consolidada (snapshots ganhas)
            // Não é fórmula de comissão — apenas soma o que o pipeline calculou (DD-003)
            long projected = lines.Where(l => !l.IsSnapshot).Sum(l => l.CommissionCents);
            long consolidated = lines.Where(l => l.IsSnapshot).Sum(l => l.CommissionCents);

            return new CommissionViewResult(
                PartnerId: query.PartnerId,
                PeriodFrom: query.From,
                PeriodTo: query.To,
                ProjectedCommissionCents: projected,
                ConsolidatedCommissionCents: consolidated,
                CommissionUnavailable: false);
        }
        finally
        {
            stopwatch.Stop();
            metrics.RecordCommissionViewDuration(stopwatch.Elapsed);
        }
    }
}
