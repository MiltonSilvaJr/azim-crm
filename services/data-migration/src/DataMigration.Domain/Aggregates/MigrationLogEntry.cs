namespace DataMigration.Domain.Aggregates;

/// <summary>
/// Entidade filha do agregado <see cref="MigrationJob"/>. Representa o resultado
/// do processamento de uma linha da planilha durante o dry-run ou o import.
///
/// Invariantes (design §4.2, RNF 3):
///   - Sem PII: <c>Message</c> nunca contém nome, e-mail ou telefone de contato.
///   - Referência por índice, nunca por dado pessoal (<c>SourceRowIndex</c>).
///   - <c>ImportKey</c> é a chave de idempotência por linha (DD-003); pode ser nula no dry-run.
///
/// Rastreia: design §4.2, §7 (<c>migration_logs</c>), RNF 3, DD-003, TASK-03.
/// </summary>
public sealed class MigrationLogEntry
{
    /// <summary>Identificador único da entrada de log.</summary>
    public Guid Id { get; }

    /// <summary>Identificador do job ao qual esta entrada pertence.</summary>
    public Guid MigrationJobId { get; }

    /// <summary>
    /// Aba de origem na planilha: <c>"pipeline"</c> ou <c>"acoes_comerciais"</c>.
    /// </summary>
    public string SourceSheet { get; }

    /// <summary>
    /// Índice base-0 da linha na aba. Usado como referência de rastreabilidade;
    /// nunca como dado de PII.
    /// </summary>
    public int SourceRowIndex { get; }

    /// <summary>Status do processamento desta linha.</summary>
    public MigrationLogStatus Status { get; }

    /// <summary>
    /// Mensagem técnica descritiva (sem PII — RNF 3).
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Chave de idempotência por linha (DD-003). Nula durante dry-run;
    /// preenchida no import para suportar reexecução segura.
    /// </summary>
    public string? ImportKey { get; }

    /// <summary>Data/hora UTC em que a entrada foi criada.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Cria uma entrada de log de processamento de linha.
    /// </summary>
    /// <param name="migrationJobId">ID do job pai.</param>
    /// <param name="sourceSheet">Aba de origem (<c>"pipeline"</c> ou <c>"acoes_comerciais"</c>).</param>
    /// <param name="sourceRowIndex">Índice base-0 da linha.</param>
    /// <param name="status">Status de processamento.</param>
    /// <param name="message">Mensagem técnica sem PII.</param>
    /// <param name="importKey">Chave de idempotência por linha (opcional no dry-run).</param>
    /// <param name="createdAt">Instante de criação (injetado para testabilidade).</param>
    internal MigrationLogEntry(
        Guid migrationJobId,
        string sourceSheet,
        int sourceRowIndex,
        MigrationLogStatus status,
        string message,
        string? importKey,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(sourceSheet))
        {
            throw new ArgumentException("sourceSheet é obrigatório.", nameof(sourceSheet));
        }

        if (sourceRowIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRowIndex), "Índice de linha deve ser ≥ 0.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("message é obrigatório.", nameof(message));
        }

        Id = Guid.NewGuid();
        MigrationJobId = migrationJobId;
        SourceSheet = sourceSheet;
        SourceRowIndex = sourceRowIndex;
        Status = status;
        Message = message;
        ImportKey = importKey;
        CreatedAt = createdAt;
    }
}
