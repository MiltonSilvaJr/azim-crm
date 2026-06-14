using DataMigration.Domain.Exceptions;

namespace DataMigration.Domain.Aggregates;

/// <summary>
/// Aggregate Root do módulo data-migration. Representa uma tentativa de migração
/// e seu ciclo de vida completo.
///
/// Invariantes (design §4.1):
///   - Transições de estado ocorrem apenas por métodos do agregado.
///   - Transições inválidas lançam <see cref="InvalidMigrationStateTransitionException"/> (MIG-ERR-005).
///   - A transição para <c>Importing</c> requer zero oportunidades sem owner (PBT-07, MIG-ERR-006).
///   - Metadados do arquivo são imutáveis após <c>created</c>.
///   - <c>Completed</c> e <c>Failed</c> são terminais.
///
/// Rastreia: design §4.1, §4.5, TASK-03; Req 4, 6, 12.
/// </summary>
public sealed class MigrationJob
{
    // =========================================================================
    // Estado mutável interno
    // =========================================================================

    private readonly List<MigrationLogEntry> _logEntries = new();

    // =========================================================================
    // Identidade e metadados imutáveis (design §4.1)
    // =========================================================================

    /// <summary>Identificador único do job.</summary>
    public Guid Id { get; }

    /// <summary>Tenant ao qual este job pertence (ADR-0001).</summary>
    public Guid TenantId { get; }

    /// <summary>Nome original do arquivo enviado pelo usuário.</summary>
    public string SourceFileName { get; }

    /// <summary>Tamanho em bytes do arquivo enviado.</summary>
    public long SourceFileSizeBytes { get; }

    /// <summary>Hash SHA-256 do arquivo, base para auditoria de congelamento (Req 13).</summary>
    public string SourceFileHash { get; }

    /// <summary>Número de linhas detectadas no parsing inicial.</summary>
    public int DetectedRowCount { get; }

    /// <summary>Usuário (Platform Operator) que iniciou o job.</summary>
    public Guid CreatedBy { get; }

    /// <summary>Instante de criação do job (UTC).</summary>
    public DateTimeOffset CreatedAt { get; }

    // =========================================================================
    // Estado da máquina de estados
    // =========================================================================

    /// <summary>Estado atual do job.</summary>
    public MigrationJobStatus Status { get; private set; }

    // =========================================================================
    // Snapshots JSONB (design §4.1, DD-007)
    // =========================================================================

    /// <summary>
    /// Snapshot do relatório de dry-run (design §7, <c>triage_report</c>).
    /// Sem PII; populado após <c>dry_run_completed</c>.
    /// </summary>
    public string? TriageReportJson { get; private set; }

    /// <summary>
    /// Snapshot do progresso de triagem (design §7, <c>triage_resolution</c>).
    /// Sem PII; salvo/retomado entre sessões (Req 4.4, DD-007).
    /// </summary>
    public string? TriageResolutionJson { get; private set; }

    /// <summary>
    /// Relatório final do import (design §7, <c>import_report</c>).
    /// Disponível apenas em estado <c>Completed</c>.
    /// </summary>
    public string? ImportReportJson { get; private set; }

    // =========================================================================
    // Timestamps de import (RNF 1.2)
    // =========================================================================

    /// <summary>Instante de início do import (UTC). Nulo antes de <c>importing</c>.</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>Instante de término do import (UTC). Nulo antes de <c>completed</c>/<c>rolled_back</c>.</summary>
    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Instante de última atualização (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    // =========================================================================
    // Log de processamento de linhas
    // =========================================================================

    /// <summary>
    /// Entradas de log de processamento de linhas — somente leitura via coleção.
    /// Rastreia: design §4.2, <c>migration_logs</c>.
    /// </summary>
    public IReadOnlyList<MigrationLogEntry> LogEntries => _logEntries.AsReadOnly();

    // =========================================================================
    // Construtor privado (fábrica estática obrigatória)
    // =========================================================================

    private MigrationJob(
        Guid id,
        Guid tenantId,
        string sourceFileName,
        long sourceFileSizeBytes,
        string sourceFileHash,
        int detectedRowCount,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        SourceFileName = sourceFileName;
        SourceFileSizeBytes = sourceFileSizeBytes;
        SourceFileHash = sourceFileHash;
        DetectedRowCount = detectedRowCount;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Status = MigrationJobStatus.Created;
    }

    // =========================================================================
    // Fábrica estática
    // =========================================================================

    /// <summary>
    /// Cria um novo <see cref="MigrationJob"/> em estado <c>Created</c>.
    /// </summary>
    /// <param name="tenantId">Tenant proprietário (ADR-0001).</param>
    /// <param name="sourceFileName">Nome do arquivo enviado.</param>
    /// <param name="sourceFileSizeBytes">Tamanho em bytes do arquivo.</param>
    /// <param name="sourceFileHash">Hash SHA-256 do arquivo.</param>
    /// <param name="detectedRowCount">Linhas detectadas no parsing inicial.</param>
    /// <param name="createdBy">Usuário que criou o job.</param>
    /// <param name="createdAt">Instante de criação (padrão: agora em UTC).</param>
    public static MigrationJob Create(
        Guid tenantId,
        string sourceFileName,
        long sourceFileSizeBytes,
        string sourceFileHash,
        int detectedRowCount,
        Guid createdBy,
        DateTimeOffset? createdAt = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("tenantId é obrigatório.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            throw new ArgumentException("sourceFileName é obrigatório.", nameof(sourceFileName));
        }

        if (sourceFileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceFileSizeBytes), "Tamanho deve ser > 0.");
        }

        if (string.IsNullOrWhiteSpace(sourceFileHash))
        {
            throw new ArgumentException("sourceFileHash é obrigatório.", nameof(sourceFileHash));
        }

        if (detectedRowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(detectedRowCount), "Contagem de linhas deve ser ≥ 0.");
        }

        if (createdBy == Guid.Empty)
        {
            throw new ArgumentException("createdBy é obrigatório.", nameof(createdBy));
        }

        return new MigrationJob(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            sourceFileName: sourceFileName,
            sourceFileSizeBytes: sourceFileSizeBytes,
            sourceFileHash: sourceFileHash,
            detectedRowCount: detectedRowCount,
            createdBy: createdBy,
            createdAt: createdAt ?? DateTimeOffset.UtcNow);
    }

    // =========================================================================
    // Máquina de estados (design §4.5)
    // =========================================================================

    /// <summary>
    /// Realiza a transição para o estado <paramref name="newStatus"/>.
    /// Lança <see cref="InvalidMigrationStateTransitionException"/> (MIG-ERR-005)
    /// se a transição não for permitida pela máquina de estados.
    ///
    /// Para a transição para <c>Importing</c>, use <see cref="TransitionToImporting"/>
    /// que verifica a invariante de owner obrigatório (PBT-07).
    /// </summary>
    public void TransitionTo(MigrationJobStatus newStatus, DateTimeOffset? at = null)
    {
        if (!IsValidTransition(Status, newStatus))
        {
            throw new InvalidMigrationStateTransitionException(Status, newStatus);
        }

        ApplyTransition(newStatus, at ?? DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Transição especializada para <c>Importing</c>, que verifica a invariante
    /// de owner obrigatório (PBT-07, design §4.1).
    ///
    /// Lança <see cref="OwnerRequiredForImportException"/> (MIG-ERR-006) se
    /// <paramref name="ownerlessCandidateCount"/> > 0.
    ///
    /// Lança <see cref="InvalidMigrationStateTransitionException"/> (MIG-ERR-005)
    /// se o estado atual não for <c>ReadyToImport</c>.
    /// </summary>
    /// <param name="ownerlessCandidateCount">Número de oportunidades sem owner atribuído.</param>
    /// <param name="at">Instante da transição (injetado para testabilidade).</param>
    public void TransitionToImporting(int ownerlessCandidateCount, DateTimeOffset? at = null)
    {
        if (!IsValidTransition(Status, MigrationJobStatus.Importing))
        {
            throw new InvalidMigrationStateTransitionException(Status, MigrationJobStatus.Importing);
        }

        if (ownerlessCandidateCount > 0)
        {
            throw new OwnerRequiredForImportException(ownerlessCandidateCount);
        }

        var now = at ?? DateTimeOffset.UtcNow;
        StartedAt = now;
        ApplyTransition(MigrationJobStatus.Importing, now);
    }

    /// <summary>
    /// Verifica se a transição para <c>Importing</c> é permitida pela invariante de owner.
    /// Retorna <c>true</c> quando <paramref name="ownerlessCandidateCount"/> == 0.
    ///
    /// Rastreia: PBT-07, design §4.5.
    /// </summary>
    public bool CanTransitionToImporting(int ownerlessCandidateCount) =>
        ownerlessCandidateCount == 0;

    // =========================================================================
    // Snapshots JSONB (design §4.1, DD-007)
    // =========================================================================

    /// <summary>Persiste o relatório do dry-run no agregado.</summary>
    public void SetTriageReport(string triageReportJson, DateTimeOffset? at = null)
    {
        if (string.IsNullOrWhiteSpace(triageReportJson))
        {
            throw new ArgumentException("triageReportJson é obrigatório.", nameof(triageReportJson));
        }

        TriageReportJson = triageReportJson;
        UpdatedAt = at ?? DateTimeOffset.UtcNow;
    }

    /// <summary>Persiste o snapshot de progresso da triagem (Req 4.4, DD-007).</summary>
    public void SetTriageResolution(string triageResolutionJson, DateTimeOffset? at = null)
    {
        if (string.IsNullOrWhiteSpace(triageResolutionJson))
        {
            throw new ArgumentException("triageResolutionJson é obrigatório.", nameof(triageResolutionJson));
        }

        TriageResolutionJson = triageResolutionJson;
        UpdatedAt = at ?? DateTimeOffset.UtcNow;
    }

    /// <summary>Persiste o relatório final do import (Req 11).</summary>
    public void SetImportReport(string importReportJson, DateTimeOffset? at = null)
    {
        if (string.IsNullOrWhiteSpace(importReportJson))
        {
            throw new ArgumentException("importReportJson é obrigatório.", nameof(importReportJson));
        }

        ImportReportJson = importReportJson;
        FinishedAt = at ?? DateTimeOffset.UtcNow;
        UpdatedAt = FinishedAt.Value;
    }

    // =========================================================================
    // Log de processamento
    // =========================================================================

    /// <summary>
    /// Registra a entrada de processamento de uma linha da planilha.
    /// Mensagem não deve conter PII (RNF 3).
    /// </summary>
    public void AddLogEntry(
        string sourceSheet,
        int sourceRowIndex,
        MigrationLogStatus status,
        string message,
        string? importKey,
        DateTimeOffset? at = null)
    {
        var entry = new MigrationLogEntry(
            migrationJobId: Id,
            sourceSheet: sourceSheet,
            sourceRowIndex: sourceRowIndex,
            status: status,
            message: message,
            importKey: importKey,
            createdAt: at ?? DateTimeOffset.UtcNow);

        _logEntries.Add(entry);
    }

    // =========================================================================
    // Implementação da máquina de estados
    // =========================================================================

    /// <summary>
    /// Tabela de transições válidas (design §4.5).
    /// Chave: (estadoAtual, estadoDestino) → permitido.
    /// </summary>
    private static readonly HashSet<(MigrationJobStatus From, MigrationJobStatus To)> ValidTransitions =
    [
        (MigrationJobStatus.Created,           MigrationJobStatus.DryRunCompleted),
        (MigrationJobStatus.Created,           MigrationJobStatus.Failed),
        (MigrationJobStatus.DryRunCompleted,   MigrationJobStatus.TriageInProgress),
        (MigrationJobStatus.DryRunCompleted,   MigrationJobStatus.Failed),
        (MigrationJobStatus.TriageInProgress,  MigrationJobStatus.TriageInProgress),
        (MigrationJobStatus.TriageInProgress,  MigrationJobStatus.ReadyToImport),
        (MigrationJobStatus.ReadyToImport,     MigrationJobStatus.TriageInProgress),
        (MigrationJobStatus.ReadyToImport,     MigrationJobStatus.Importing),
        (MigrationJobStatus.Importing,         MigrationJobStatus.Completed),
        (MigrationJobStatus.Importing,         MigrationJobStatus.RolledBack),
        (MigrationJobStatus.RolledBack,        MigrationJobStatus.TriageInProgress),
        // Completed e Failed: terminais — sem saída.
    ];

    private static bool IsValidTransition(MigrationJobStatus from, MigrationJobStatus to) =>
        ValidTransitions.Contains((from, to));

    private void ApplyTransition(MigrationJobStatus newStatus, DateTimeOffset at)
    {
        Status = newStatus;
        UpdatedAt = at;
    }
}
