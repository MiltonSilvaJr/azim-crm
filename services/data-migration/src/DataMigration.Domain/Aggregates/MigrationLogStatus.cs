namespace DataMigration.Domain.Aggregates;

/// <summary>
/// Status de processamento de uma linha da planilha no <see cref="MigrationLogEntry"/>.
///
/// Rastreia: design §7 (<c>ck_migration_log_status</c>), RNF 3 (sem PII nas mensagens).
/// </summary>
public enum MigrationLogStatus
{
    /// <summary>Linha processada com sucesso; sem pendências.</summary>
    Ok,

    /// <summary>
    /// Linha processada com ressalva não bloqueante (ex: atividade sem oportunidade
    /// correspondente — Req 10.4). Import não é abortado.
    /// </summary>
    Aviso,

    /// <summary>Linha com erro que abortou o import e disparou rollback total.</summary>
    Erro,
}
