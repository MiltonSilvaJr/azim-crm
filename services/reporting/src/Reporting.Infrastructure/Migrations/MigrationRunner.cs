using System.Reflection;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Reporting.Infrastructure.Migrations;

/// <summary>
/// Aplica as migrations SQL do módulo reporting em ordem numérica.
///
/// Estratégia: tabela <c>reporting_migrations</c> como controle de migrations aplicadas.
/// Cada script SQL é lido do diretório de migrations e aplicado uma única vez (idempotência
/// garantida por <c>IF NOT EXISTS</c> e <c>CREATE OR REPLACE VIEW</c> nos próprios scripts).
///
/// Mapeia: TASK-13..TASK-17, design §7.2, §7.4, ADR-0001.
/// </summary>
public sealed class MigrationRunner
{
    private readonly string _connectionString;
    private readonly ILogger<MigrationRunner> _logger;

    // Diretório de migrations relativo ao assembly (embedded ou do disco).
    private static readonly string MigrationsDir = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "Migrations");

    /// <summary>Inicializa o runner com a connection string e logger.</summary>
    public MigrationRunner(string connectionString, ILogger<MigrationRunner> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(logger);
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// Aplica todas as migrations SQL pendentes em ordem numérica (M001..MNNN).
    /// Idempotente: scripts já aplicados são ignorados.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureMigrationTableAsync(connection, cancellationToken);

        var appliedMigrations = await GetAppliedMigrationsAsync(connection, cancellationToken);
        var scripts = GetMigrationScripts();

        foreach (var (name, sql) in scripts)
        {
            if (appliedMigrations.Contains(name))
            {
                _logger.LogDebug("Migration {Name} já aplicada — ignorando.", name);
                continue;
            }

            _logger.LogInformation("Aplicando migration {Name}.", name);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await connection.ExecuteAsync(sql, transaction: transaction);
                await connection.ExecuteAsync(
                    "INSERT INTO reporting_migrations (name, applied_at) VALUES (@name, NOW())",
                    new { name },
                    transaction: transaction);
                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation("Migration {Name} aplicada com sucesso.", name);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Falha ao aplicar migration {Name}.", name);
                throw;
            }
        }
    }

    private static async Task EnsureMigrationTableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS reporting_migrations (
                name        TEXT        NOT NULL PRIMARY KEY,
                applied_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """;
        await connection.ExecuteAsync(sql);
    }

    private static async Task<HashSet<string>> GetAppliedMigrationsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var names = await connection.QueryAsync<string>(
            "SELECT name FROM reporting_migrations ORDER BY applied_at");
        return names.ToHashSet();
    }

    /// <summary>
    /// Lê os scripts SQL do diretório de migrations e os retorna em ordem numérica (M001, M002...).
    /// Suporta tanto leitura do disco (dev/test) quanto scripts inline (quando não há diretório).
    /// </summary>
    private static IEnumerable<(string Name, string Sql)> GetMigrationScripts()
    {
        if (Directory.Exists(MigrationsDir))
        {
            return Directory
                .GetFiles(MigrationsDir, "M*.sql")
                .OrderBy(f => Path.GetFileNameWithoutExtension(f))
                .Select(f => (Path.GetFileNameWithoutExtension(f), File.ReadAllText(f)));
        }

        // Fallback: scripts embutidos inline para ambientes sem diretório de migrations
        return GetInlineScripts();
    }

    /// <summary>
    /// Scripts SQL inline para uso em testes de integração (Testcontainers).
    /// Sincronizados manualmente com os arquivos .sql de migrations.
    /// Mapeia: TASK-13..TASK-17.
    /// </summary>
    public static IEnumerable<(string Name, string Sql)> GetInlineScripts()
    {
        yield return ("M001_CreateViewFunnelReport", SqlScripts.M001_CreateViewFunnelReport);
        yield return ("M002_CreateViewForecastReport", SqlScripts.M002_CreateViewForecastReport);
        yield return ("M003_CreateViewRankingReport", SqlScripts.M003_CreateViewRankingReport);
        yield return ("M004_CreateViewChannelReport", SqlScripts.M004_CreateViewChannelReport);
        yield return ("M005_CreateViewCommissionReport", SqlScripts.M005_CreateViewCommissionReport);
        yield return ("M006_CreateCriticalIndexes", SqlScripts.M006_CreateCriticalIndexes);
    }
}
