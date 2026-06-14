namespace DataMigration.Domain.Aggregates;

/// <summary>
/// Estados da máquina de estados do agregado <see cref="MigrationJob"/>.
///
/// Fluxo canônico (design §4.5):
///   created → dry_run_completed → triage_in_progress → ready_to_import
///             → importing → completed | rolled_back | failed
///
/// <c>completed</c> e <c>failed</c> são terminais (sem transição de saída).
/// <c>rolled_back</c> admite retorno para <c>triage_in_progress</c>.
///
/// Rastreia: design §4.5, TASK-03.
/// </summary>
public enum MigrationJobStatus
{
    /// <summary>
    /// Job criado; arquivo enviado e metadados persistidos. Estado inicial.
    /// </summary>
    Created,

    /// <summary>
    /// Dry-run concluído; <c>TriageReport</c> disponível para triagem assistida.
    /// </summary>
    DryRunCompleted,

    /// <summary>
    /// Triagem em andamento; owners, estágios e dedupes sendo resolvidos.
    /// Admite <c>salvar/retomar</c> (transição para si mesmo) e retorno de <c>rolled_back</c>.
    /// </summary>
    TriageInProgress,

    /// <summary>
    /// Todas as pendências obrigatórias resolvidas; import liberado para execução.
    /// </summary>
    ReadyToImport,

    /// <summary>
    /// Import em execução na transação única do PostgreSQL.
    /// </summary>
    Importing,

    /// <summary>
    /// Import concluído com sucesso; dados persistidos; estado terminal.
    /// </summary>
    Completed,

    /// <summary>
    /// Falha durante o import; rollback total executado. Admite nova tentativa
    /// via retorno para <c>triage_in_progress</c>.
    /// </summary>
    RolledBack,

    /// <summary>
    /// Falha de validação/parsing antes do import; estado terminal.
    /// </summary>
    Failed,
}
