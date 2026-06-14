namespace DataMigration.Infrastructure.Outbox;

/// <summary>
/// Tipos de eventos do Outbox (tabela <c>outbox_events</c>).
///
/// Seguem convenção de evento de domínio no passado (fato ocorrido).
/// Sem PII no nome do evento.
///
/// Rastreia: design §6.3, §6.6, §9, RNF 4, TASK-20.
/// </summary>
public static class OutboxEventType
{
    /// <summary>Import concluído com sucesso — commit da transação.</summary>
    public const string ImportCompleted = "data_migration.import_completed";

    /// <summary>Dry-run concluído — sem efeito colateral (Req 2.1).</summary>
    public const string DryRunCompleted = "data_migration.dry_run_completed";

    /// <summary>Import revertido por falha — rollback da transação.</summary>
    public const string ImportRolledBack = "data_migration.import_rolled_back";
}
