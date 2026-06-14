using DataMigration.Domain.Aggregates;

namespace DataMigration.Infrastructure.Persistence;

/// <summary>
/// Extensões de conversão entre <see cref="MigrationLogStatus"/> e string snake_case
/// para persistência na coluna <c>status VARCHAR(10)</c> da tabela <c>migration_logs</c>.
///
/// Rastreia: design §7, TASK-15.
/// </summary>
internal static class MigrationLogStatusExtensions
{
    public static string ToSnakeCase(this MigrationLogStatus status) => status switch
    {
        MigrationLogStatus.Ok => "ok",
        MigrationLogStatus.Aviso => "aviso",
        MigrationLogStatus.Erro => "erro",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static MigrationLogStatus FromSnakeCase(string value) => value switch
    {
        "ok" => MigrationLogStatus.Ok,
        "aviso" => MigrationLogStatus.Aviso,
        "erro" => MigrationLogStatus.Erro,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
