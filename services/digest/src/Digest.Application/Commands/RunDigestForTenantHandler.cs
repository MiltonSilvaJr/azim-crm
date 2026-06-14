using Digest.Application.Queries;
using Digest.Domain.Aggregates;
using MediatR;
using NodaTime;

namespace Digest.Application.Commands;

/// <summary>
/// Handler de <see cref="RunDigestForTenantCommand"/>.
/// Orquestra o <see cref="DigestJob"/>: seta o contexto de tenant, seleciona destinatários,
/// e dispara <see cref="SendUserDigestCommand"/> para cada candidato.
/// Falha de um usuário não interrompe os demais (RNF 5.3 — try/catch por usuário).
/// </summary>
public sealed class RunDigestForTenantHandler : IRequestHandler<RunDigestForTenantCommand, RunDigestForTenantResult>
{
    private readonly IMediator _mediator;
    private readonly IDateTimeZoneProvider _zoneProvider;

    /// <summary>
    /// Constrói o handler.
    /// </summary>
    public RunDigestForTenantHandler(IMediator mediator, IDateTimeZoneProvider zoneProvider)
    {
        _mediator = mediator;
        _zoneProvider = zoneProvider;
    }

    /// <inheritdoc/>
    public async Task<RunDigestForTenantResult> Handle(
        RunDigestForTenantCommand request,
        CancellationToken cancellationToken)
    {
        // Seleciona destinatários para o tenant e data
        var selectQuery = new SelectRecipientsQuery(
            request.TenantId,
            new Domain.ValueObjects.DigestDate(
                // Converte ReferenceUtc para data local do tenant.
                // Para simplificar (digestDate já derivada no trigger), usa UTC date como fallback.
                // A data correta é derivada pelo trigger via TenantEligibility.ToDigestDate().
                LocalDate.FromDateTime(request.ReferenceUtc.UtcDateTime)));

        var recipients = await _mediator.Send(selectQuery, cancellationToken);

        var sent = 0;
        var skipped = 0;
        var failed = 0;

        // Por cada candidato — falha de um não interrompe os demais (RNF 5.3)
        foreach (var candidate in recipients)
        {
            try
            {
                // Email placeholder — será fornecido pela Infrastructure via IUserDirectoryPort
                // Na Onda 3 o e-mail do usuário é encapsulado no comando; a Infrastructure o resolve
                var sendCommand = new SendUserDigestCommand(
                    TenantId: request.TenantId,
                    UserId: candidate.UserId,
                    DigestDate: selectQuery.DigestDate,
                    UserEmail: $"user-{candidate.UserId:N}@placeholder.internal", // resolvido na Onda 4
                    CorrelationId: request.CorrelationId);

                var result = await _mediator.Send(sendCommand, cancellationToken);

                if (result.Sent) sent++;
                else if (result.Skipped) skipped++;
                else failed++;
            }
            catch (Exception)
            {
                // Falha de um usuário não interrompe os demais (RNF 5.3)
                // Log e métrica são responsabilidade do LoggingBehavior
                failed++;
            }
        }

        return new RunDigestForTenantResult(
            RecipientsSelected: recipients.Count,
            EmailsSent: sent,
            EmailsSkipped: skipped,
            EmailsFailed: failed);
    }
}
