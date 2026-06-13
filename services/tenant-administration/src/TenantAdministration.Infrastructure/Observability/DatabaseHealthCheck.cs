using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TenantAdministration.Infrastructure.Persistence;

namespace TenantAdministration.Infrastructure.Observability;

/// <summary>
/// Health check de conectividade com o banco de dados PostgreSQL (Cloud SQL).
/// Usado no endpoint <c>/health/ready</c> (TASK-22, design.md §11).
/// Falha causa retorno 503 no readiness check.
/// </summary>
public sealed class DatabaseHealthCheck(TenantAdministrationDbContext dbContext)
    : IHealthCheck
{
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Executa query mínima para verificar conectividade
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Banco de dados acessível.")
                : HealthCheckResult.Unhealthy("Não foi possível conectar ao banco de dados.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Erro ao verificar conectividade com o banco de dados.",
                exception: ex);
        }
    }
}
