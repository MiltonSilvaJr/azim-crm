using Digest.Application.Commands;
using MediatR;

namespace Digest.Api.Infrastructure;

/// <summary>
/// Implementação MVP de <see cref="IPubSubFanout"/> que publica mensagens em processo
/// (despacha <see cref="RunDigestForTenantCommand"/> diretamente via MediatR sem Pub/Sub real).
/// Em produção: substituir por implementação com Google Cloud Pub/Sub SDK
/// que publica mensagens no tópico <c>azim-digest-fanout</c> (DD-005, design §6.3).
/// </summary>
/// <remarks>
/// Esta implementação é intencional para o MVP monolítico: o worker ainda não tem
/// o SDK do Pub/Sub configurado. O PerTenantConsumer (TASK-22) é o consumer real
/// do tópico em ambiente GCP.
/// </remarks>
public sealed class InProcessFanout : IPubSubFanout
{
    private readonly IMediator _mediator;
    private readonly ILogger<InProcessFanout> _logger;

    /// <summary>Constrói o fanout em processo com MediatR.</summary>
    public InProcessFanout(IMediator mediator, ILogger<InProcessFanout> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PublishFanoutMessagesAsync(
        IReadOnlyList<Guid> eligibleTenants,
        DateTimeOffset referenceUtc,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        if (eligibleTenants.Count == 0)
        {
            _logger.LogInformation(
                "Fan-out: nenhum tenant elegível para {ReferenceUtc}. CorrelationId: {CorrelationId}",
                referenceUtc, correlationId);
            return;
        }

        // Despacha um comando por tenant (equivale à mensagem Pub/Sub por tenant — DD-005)
        // Em produção: substituir por publicação no tópico e deixar o PerTenantConsumer processar
        foreach (var tenantId in eligibleTenants)
        {
            try
            {
                // Fire-and-forget: o processamento de um tenant não bloqueia os demais (RNF 5.3)
                _ = _mediator.Send(
                    new RunDigestForTenantCommand(tenantId, referenceUtc, correlationId),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Falha de um tenant não propaga (RNF 5.3)
                _logger.LogError(ex,
                    "Fan-out: erro ao despachar tenant {TenantId}. CorrelationId: {CorrelationId}",
                    tenantId, correlationId);
            }
        }
    }
}
