using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using AuditLog.Infrastructure.Persistence;
using Npgsql;

namespace AuditLog.Infrastructure.HealthChecks;

/// <summary>
/// Health check de capacidade de INSERT em <c>audit_logs</c> (design §11.5, RNF-006).
/// <para>
/// Verifica dois aspectos:
/// <list type="number">
/// <item>Conectividade com o banco de dados (RNF-006.1).</item>
/// <item>Permissão de INSERT para o role <c>app</c> (RNF-006.2).</item>
/// </list>
/// </para>
/// <para>
/// Estados retornados:
/// <list type="bullet">
/// <item><see cref="HealthStatus.Healthy"/> — banco disponível e INSERT permitido.</item>
/// <item><see cref="HealthStatus.Unhealthy"/> — banco indisponível ou não conectável.</item>
/// <item><see cref="HealthStatus.Degraded"/> — banco disponível mas INSERT sem permissão.</item>
/// </list>
/// </para>
/// </summary>
public sealed class AuditInsertCapabilityHealthCheck : IHealthCheck
{
    private readonly AuditLogDbContext _dbContext;

    /// <summary>Inicializa o health check com o DbContext do módulo.</summary>
    public AuditInsertCapabilityHealthCheck(AuditLogDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // 1. Verifica conectividade
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy(
                    "Banco de dados inacessível: a connection ao PostgreSQL falhou.",
                    data: new Dictionary<string, object> { ["check"] = "connectivity" });
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Erro ao verificar conectividade com o banco de dados.",
                exception: ex,
                data: new Dictionary<string, object> { ["check"] = "connectivity" });
        }

        // 2. Verifica permissão de INSERT via INSERT em transação revertida
        // Usa conexão direta (Npgsql) para garantir que SET LOCAL e INSERT compartilhem a mesma sessão.
        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                // SET LOCAL app.tenant_id para satisfazer a policy de RLS (FORCE ROW LEVEL SECURITY)
                await using var setCmd = connection.CreateCommand();
                setCmd.Transaction = transaction;
                setCmd.CommandText = "SET LOCAL app.tenant_id = '00000000-0000-0000-0000-000000000001'";
                await setCmd.ExecuteNonQueryAsync(cancellationToken);

                // Tenta INSERT com valores mínimos válidos (id gerado via gen_random_uuid)
                await using var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = """
                    INSERT INTO audit_logs (id, tenant_id, user_id, entity_type, entity_id, action, delta_json)
                    VALUES (
                        gen_random_uuid(),
                        '00000000-0000-0000-0000-000000000001'::uuid,
                        '00000000-0000-0000-0000-000000000002'::uuid,
                        'HealthCheck',
                        '00000000-0000-0000-0000-000000000003'::uuid,
                        'create',
                        '{"check":"health"}'::jsonb
                    )
                    """;
                await insertCmd.ExecuteNonQueryAsync(cancellationToken);

                // Reverte — o INSERT de health check nunca deve persistir
                await transaction.RollbackAsync(cancellationToken);

                return HealthCheckResult.Healthy(
                    "Banco disponível e permissão de INSERT confirmada.",
                    data: new Dictionary<string, object> { ["check"] = "insert_capability" });
            }
            catch
            {
                try { await transaction.RollbackAsync(cancellationToken); } catch { /* ignora */ }
                throw;
            }
        }
        catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "42501") // insufficient_privilege
        {
            return HealthCheckResult.Degraded(
                "Banco disponível, mas o role atual não possui permissão de INSERT em audit_logs.",
                exception: pgEx,
                data: new Dictionary<string, object> { ["check"] = "insert_permission", ["sqlstate"] = pgEx.SqlState });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded(
                "Banco disponível, mas a verificação de capacidade de INSERT falhou.",
                exception: ex,
                data: new Dictionary<string, object> { ["check"] = "insert_capability", ["error_type"] = ex.GetType().Name });
        }
    }
}
