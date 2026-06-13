using Authentication.Application.Ports;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PortHealthStatus = Authentication.Application.Ports.Results.HealthStatus;

namespace Authentication.Infrastructure.HealthChecks;

/// <summary>
/// Health check do provedor de identidade (GCP Identity Platform).
///
/// Usado no endpoint <c>GET /health/ready</c> para sinalizar ao orquestrador (Cloud Run)
/// que o pod está pronto para receber tráfego. Se o IdP estiver indisponível, o pod
/// sai da rotação até que o IdP se recupere (RNF 3.2, design.md § 11).
///
/// Nunca expõe detalhe interno (stack trace, mensagem de exceção bruta do SDK)
/// na resposta HTTP (design.md § 11, security).
///
/// Mapeia: TASK-21, RNF 3.2, RISK-AUTH-01, design.md § 11.
/// </summary>
public sealed class IdentityProviderHealthCheck : IHealthCheck
{
    private readonly IIdentityProvider _identityProvider;

    /// <summary>
    /// Inicializa o health check com o provider de identidade.
    /// </summary>
    /// <param name="identityProvider">Porta do provedor de identidade.</param>
    public IdentityProviderHealthCheck(IIdentityProvider identityProvider)
    {
        _identityProvider = identityProvider;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await _identityProvider.HealthCheckAsync(cancellationToken);

            return status switch
            {
                PortHealthStatus.Healthy => HealthCheckResult.Healthy(),
                PortHealthStatus.Degraded => HealthCheckResult.Degraded(
                    "Identity provider está degradado."),
                _ => HealthCheckResult.Unhealthy(
                    "Identity provider não disponível.")
            };
        }
        catch
        {
            // Nunca propaga exceção nem expõe mensagem interna do SDK
            return HealthCheckResult.Unhealthy(
                "Identity provider não disponível.");
        }
    }
}
