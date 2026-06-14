using Microsoft.Extensions.Diagnostics.HealthChecks;
using PartnerManagement.Application.Ports;

namespace PartnerManagement.Infrastructure.Observability;

/// <summary>
/// Health check do read port de comissões (<see cref="IPartnerCommissionReadPort"/>).
/// Verifica a disponibilidade do opportunity-pipeline, registrado como "partner_commission_read_port".
/// Falha do read port resulta em <c>Degraded</c> (não <c>Unhealthy</c>) — o cadastro continua operacional.
/// Mapeia: design §11, TASK-26, RISK-PM-06.
/// </summary>
public sealed class PartnerReadPortHealthCheck : IHealthCheck
{
    private readonly IPartnerCommissionReadPort _readPort;

    /// <summary>
    /// Inicializa o health check com a porta de leitura de comissões.
    /// </summary>
    /// <param name="readPort">Porta de leitura do read model do pipeline.</param>
    public PartnerReadPortHealthCheck(IPartnerCommissionReadPort readPort)
    {
        _readPort = readPort;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Tenta obter linhas de comissão de um período dummy — uma coleção vazia é sucesso
            // O objetivo é verificar que o endpoint do pipeline responde sem timeout ou erro 5xx
            await _readPort.GetCommissionLinesAsync(
                tenantId: Guid.Empty,
                partnerId: Guid.Empty,
                from: DateTimeOffset.UtcNow.AddDays(-1),
                to: DateTimeOffset.UtcNow,
                correlationId: "health-check",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy("Read port de comissões disponível.");
        }
        catch (OperationCanceledException)
        {
            // Cancelamento legítimo (ex.: app shutting down) — não reportar como falha
            return HealthCheckResult.Degraded("Read port: verificação cancelada.");
        }
        catch (Exception ex)
        {
            // Falha do read port: Degraded, não Unhealthy — cadastro continua operacional (RISK-PM-06)
            return HealthCheckResult.Degraded(
                description: "Read port de comissões indisponível. Cadastro continua operacional.",
                exception: ex);
        }
    }
}
