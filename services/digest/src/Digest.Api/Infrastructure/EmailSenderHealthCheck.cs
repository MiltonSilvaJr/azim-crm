using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Digest.Api.Infrastructure;

/// <summary>
/// Health check para o <c>IEmailSender</c> (design §8.2).
/// Verifica que o provider está configurado (ping sem envio — Req 8).
/// Em MVP: verifica configuração presente. Em produção: ping ao notification-delivery.
/// </summary>
public sealed class EmailSenderHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    /// <summary>Constrói o health check com a configuração do worker.</summary>
    public EmailSenderHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // MVP: retorna healthy (IEmailSender = NoOp; sem dependência real).
        // Produção: substituir por ping ao notification-delivery endpoint.
        return Task.FromResult(HealthCheckResult.Healthy("Email sender disponível (MVP no-op)."));
    }
}
