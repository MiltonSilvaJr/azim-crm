using Microsoft.Extensions.Diagnostics.HealthChecks;
using Organization.Infrastructure.Persistence;

namespace Organization.Infrastructure.Observability;

/// <summary>
/// Health check de prontidão para PostgreSQL via <see cref="OrganizationDbContext"/>.
/// Executa <c>SELECT 1</c> para verificar conectividade.
/// Usado no endpoint <c>/health/ready</c>.
/// </summary>
public sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly OrganizationDbContext _dbContext;

    /// <summary>Inicializa o health check com o contexto de banco.</summary>
    public PostgresHealthCheck(OrganizationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.Database.CanConnectAsync(cancellationToken);
            return HealthCheckResult.Healthy("PostgreSQL disponível.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL indisponível.",
                exception: ex,
                data: new Dictionary<string, object> { ["error"] = ex.GetType().Name });
        }
    }
}
