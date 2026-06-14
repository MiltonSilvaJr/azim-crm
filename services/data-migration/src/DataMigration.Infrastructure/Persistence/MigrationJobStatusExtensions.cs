using DataMigration.Domain.Aggregates;

namespace DataMigration.Infrastructure.Persistence;

/// <summary>
/// Extensões de conversão entre <see cref="MigrationJobStatus"/> e string snake_case
/// para persistência na coluna <c>status VARCHAR(20)</c> da tabela <c>migration_jobs</c>.
///
/// Rastreia: design §7, TASK-15.
/// </summary>
internal static class MigrationJobStatusExtensions
{
    public static string ToSnakeCase(this MigrationJobStatus status) => status switch
    {
        MigrationJobStatus.Created => "created",
        MigrationJobStatus.DryRunCompleted => "dry_run_completed",
        MigrationJobStatus.TriageInProgress => "triage_in_progress",
        MigrationJobStatus.ReadyToImport => "ready_to_import",
        MigrationJobStatus.Importing => "importing",
        MigrationJobStatus.Completed => "completed",
        MigrationJobStatus.RolledBack => "rolled_back",
        MigrationJobStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static MigrationJobStatus FromSnakeCase(string value) => value switch
    {
        "created" => MigrationJobStatus.Created,
        "dry_run_completed" => MigrationJobStatus.DryRunCompleted,
        "triage_in_progress" => MigrationJobStatus.TriageInProgress,
        "ready_to_import" => MigrationJobStatus.ReadyToImport,
        "importing" => MigrationJobStatus.Importing,
        "completed" => MigrationJobStatus.Completed,
        "rolled_back" => MigrationJobStatus.RolledBack,
        "failed" => MigrationJobStatus.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
