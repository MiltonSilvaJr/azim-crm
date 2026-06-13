using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using NotificationDelivery.Contracts;

namespace NotificationDelivery.Infrastructure.HealthChecks;

/// <summary>
/// <see cref="IHealthCheck"/> que verifica a disponibilidade do provedor de e-mail
/// via ping leve, sem enviar e-mail real nem consumir cota de envio (Req 11, Req 11.2, design §11.5).
///
/// Integrado ao <c>GET /health/ready</c> do <c>azim-digest-worker</c> (TRD §14.5).
///
/// Invariantes de segurança (Req 11.3):
/// <list type="bullet">
///   <item><description>Falha reportada de forma estruturada, sem PII e sem credencial na descrição.</description></item>
///   <item><description>O ping não envia e-mail real (sem consumo de cota).</description></item>
///   <item><description>Exceção não propagada ao chamador — mapeada para <see cref="HealthStatus.Unhealthy"/>.</description></item>
/// </list>
/// </summary>
public sealed class EmailProviderHealthCheck : IHealthCheck
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailProviderHealthCheck> _logger;

    /// <summary>
    /// Constrói o health check delegando ao <see cref="IEmailSender.CheckAvailabilityAsync"/>.
    /// </summary>
    /// <param name="emailSender">Sender configurado (com resiliência aplicada).</param>
    /// <param name="logger">Logger estruturado (sem PII, sem credencial — RNF 4, Req 11.3).</param>
    public EmailProviderHealthCheck(
        IEmailSender emailSender,
        ILogger<EmailProviderHealthCheck> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _emailSender
                .CheckAvailabilityAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug(
                "Health check do provedor de e-mail: {Status}. (Req 11, design §11.5)",
                result.Status);

            return result;
        }
        catch (Exception ex)
        {
            // Exceção mapeada para Unhealthy sem PII nem credencial na descrição (Req 11.3)
            _logger.LogError(
                "Health check do provedor de e-mail falhou. " +
                "Tipo={ExceptionType}. (Req 11, RISK-NOTIF-01)",
                ex.GetType().Name);

            return HealthCheckResult.Unhealthy(
                description: "Verificação de disponibilidade do provedor de e-mail falhou.",
                exception: null, // Não expõe exceção interna (sem PII, sem credencial — Req 11.3)
                data: new Dictionary<string, object>
                {
                    ["check"] = "email_provider",
                    ["error_type"] = ex.GetType().Name
                });
        }
    }
}
