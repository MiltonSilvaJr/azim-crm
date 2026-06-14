using DataMigration.Domain.Aggregates;

namespace DataMigration.Domain.Exceptions;

/// <summary>
/// Exceção de domínio lançada quando uma transição de estado inválida é tentada
/// no agregado <see cref="MigrationJob"/>.
///
/// Código de erro: <c>MIG-ERR-005</c> (design §12).
/// HTTP mapeado: 409 Conflict (decisão da camada de API).
///
/// Rastreia: design §4.5, §12 (MIG-ERR-005), TASK-03.
/// </summary>
public sealed class InvalidMigrationStateTransitionException : Exception
{
    /// <summary>Código de erro canônico do catálogo (design §12).</summary>
    public string ErrorCode => "MIG-ERR-005";

    /// <summary>Estado atual do job no momento da tentativa de transição.</summary>
    public MigrationJobStatus FromStatus { get; }

    /// <summary>Estado de destino inválido que foi tentado.</summary>
    public MigrationJobStatus ToStatus { get; }

    /// <summary>
    /// Cria a exceção com estados de origem e destino.
    /// </summary>
    public InvalidMigrationStateTransitionException(
        MigrationJobStatus fromStatus,
        MigrationJobStatus toStatus)
        : base($"Transição de estado inválida do job: {fromStatus} → {toStatus}. " +
               $"Siga a sequência: upload → dry-run → triagem → ready → execute. (MIG-ERR-005)")
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }
}
