using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Reporting.Infrastructure.HealthChecks;

/// <summary>
/// Health check de conectividade com o Cloud SQL (PostgreSQL).
///
/// Verifica readiness do banco de dados: abre uma conexão e executa <c>SELECT 1</c>.
/// Falha isolada de conectividade retorna <see cref="HealthStatus.Unhealthy"/> sem lançar
/// exceção não tratada (degradação graciosa, RNF 7).
///
/// Nunca registra dados da conexão em logs (credenciais, host, porta) — apenas o outcome (RNF 4.2).
///
/// Mapeia: TASK-24, design §11, RNF 7, TRD §11, ADR-0001.
/// </summary>
public sealed class CloudSqlHealthCheck : IHealthCheck
{
    private readonly string _connectionString;
    private readonly ILogger<CloudSqlHealthCheck> _logger;

    /// <summary>Inicializa o health check com a connection string do Cloud SQL.</summary>
    public CloudSqlHealthCheck(string connectionString, ILogger<CloudSqlHealthCheck> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(logger);
        _connectionString = connectionString;
        _logger           = logger;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("Cloud SQL acessível.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nunca loga a connection string — apenas o tipo de erro (RNF 4.2)
            _logger.LogWarning("Health check Cloud SQL: falha de conectividade ({ErrorType})", ex.GetType().Name);
            return HealthCheckResult.Unhealthy(
                description: "Cloud SQL inacessível.",
                exception: null, // não expõe detalhes internos em produção
                data: new Dictionary<string, object> { ["error_type"] = ex.GetType().Name });
        }
    }
}
