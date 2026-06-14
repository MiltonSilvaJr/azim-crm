using Microsoft.Extensions.Logging;
using Organization.Application.Ports;

namespace Organization.Infrastructure.Observability;

/// <summary>
/// Implementação de <see cref="ILastTenantAdminAlertService"/> via log estruturado.
/// Emite <c>Warning</c> operacional quando o tenant possui apenas um TAdmin ativo.
/// Sem PII — apenas identificadores opacos (design §11, RNF 6.3, MSG-017).
/// </summary>
public sealed class LastTenantAdminAlertService : ILastTenantAdminAlertService
{
    private readonly ILogger<LastTenantAdminAlertService> _logger;

    /// <summary>Inicializa o serviço com o logger.</summary>
    public LastTenantAdminAlertService(ILogger<LastTenantAdminAlertService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task AlertLastAdminAsync(
        Guid tenantId,
        int remainingAdminCount,
        CancellationToken cancellationToken = default)
    {
        // Alerta operacional — sem PII. TenantId é identificador opaco.
        _logger.LogWarning(
            "ALERTA: Tenant {TenantId} possui apenas {RemainingAdminCount} TAdmin ativo. " +
            "Risco de tenant sem administrador (RISK-ORG-01). " +
            "Promova outro usuário para o papel TAdmin.",
            tenantId,
            remainingAdminCount);

        return Task.CompletedTask;
    }
}
