using Microsoft.Extensions.Logging;
using Npgsql;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// Interceptor de conexão que executa <c>SET app.current_tenant = @tenantId</c>
/// ao alugar uma conexão do pool, garantindo que a RLS das tabelas base seja avaliada
/// com o tenant correto (ADR-0001, DD-005).
///
/// Falha-fechada: sem tenant válido, o PostgreSQL retorna zero linhas em todas as views
/// com <c>security_invoker = true</c> e nas tabelas com RLS <c>FORCE</c>.
///
/// Mapeia: TASK-13, design §6.1, ADR-0001, DD-005, RISK-REPORT-03.
/// </summary>
public sealed class TenantConnectionInterceptor
{
    private readonly ILogger<TenantConnectionInterceptor> _logger;

    /// <summary>Inicializa o interceptor com o logger estruturado.</summary>
    public TenantConnectionInterceptor(ILogger<TenantConnectionInterceptor> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Aplica <c>SET app.current_tenant = {tenantId}</c> na conexão aberta.
    /// Deve ser chamado imediatamente após abrir a conexão, antes de qualquer query.
    /// </summary>
    /// <param name="connection">Conexão Npgsql aberta.</param>
    /// <param name="tenantId">Identificador do tenant (do JWT).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <exception cref="ArgumentException">Se <paramref name="tenantId"/> for <see cref="Guid.Empty"/>.</exception>
    public async Task ApplyAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "tenantId não pode ser Guid.Empty ao configurar app.current_tenant. (ADR-0001, DD-005, REPORT-ERR-409)",
                nameof(tenantId));
        }

        // Nunca loga o tenantId como valor sensível — apenas no nível Debug estruturado
        _logger.LogDebug("Configurando app.current_tenant na conexão PostgreSQL. (ADR-0001)");

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant', @tid, false)";
        cmd.Parameters.AddWithValue("tid", tenantId.ToString());
        await cmd.ExecuteScalarAsync(cancellationToken);
    }

    /// <summary>
    /// Remove <c>app.current_tenant</c> da conexão (limpa o contexto ao devolver ao pool).
    /// Garante que a conexão não vaza o tenant para a próxima requisição.
    /// </summary>
    public async Task ClearAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant', '', false)";
        await cmd.ExecuteScalarAsync(cancellationToken);
    }
}
