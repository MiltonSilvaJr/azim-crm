using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using DataMigration.Domain.Aggregates;
using MediatR;

namespace DataMigration.Application.Commands.Upload;

/// <summary>
/// Handler do caso de uso de upload de planilha.
///
/// Fluxo (design §5.3):
///   1. Valida extensão .xlsx (MIG-ERR-001).
///   2. Valida tamanho máximo (MIG-ERR-003).
///   3. Chama <see cref="ISpreadsheetParser"/> apenas para detectar estrutura.
///   4. Valida colunas via <see cref="SpreadsheetStructureValidator"/> (MIG-ERR-002).
///   5. Cria <see cref="MigrationJob"/> em <c>Created</c>.
///   6. Persiste via <see cref="IMigrationJobRepository"/>.
///
/// Sem efeito colateral em caso de erro (nenhuma escrita no banco).
/// Não persiste conteúdo de domínio (Req 1.4).
///
/// Rastreia: design §5.3, Req 1, MIG-ERR-001..003, TASK-08.
/// </summary>
public sealed class UploadSpreadsheetHandler
    : IRequestHandler<UploadSpreadsheetCommand, UploadSpreadsheetResult>
{
    /// <summary>
    /// Tamanho máximo de arquivo aceito (10 MB, design §15).
    /// </summary>
    public const long MaxFileSizeBytes = 10L * 1024 * 1024; // 10 MB

    private readonly ISpreadsheetParser _parser;
    private readonly IMigrationJobRepository _repository;
    private readonly IClock _clock;
    private readonly ICurrentTenantContext _tenantContext;

    /// <summary>
    /// Cria o handler com as dependências injetadas.
    /// </summary>
    public UploadSpreadsheetHandler(
        ISpreadsheetParser parser,
        IMigrationJobRepository repository,
        IClock clock,
        ICurrentTenantContext tenantContext)
    {
        _parser = parser;
        _repository = repository;
        _clock = clock;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public async Task<UploadSpreadsheetResult> Handle(
        UploadSpreadsheetCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Valida extensão .xlsx — MIG-ERR-001
        if (!request.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new MigrationDomainException(
                "MIG-ERR-001",
                "Formato de arquivo inválido; envie um .xlsx (MSG-032).");
        }

        // 2. Valida tamanho máximo — MIG-ERR-003
        if (request.FileSizeBytes > MaxFileSizeBytes)
        {
            throw new MigrationDomainException(
                "MIG-ERR-003",
                $"Arquivo excede o tamanho máximo permitido ({MaxFileSizeBytes / 1024 / 1024} MB).");
        }

        // 3. Detecta estrutura (sem persistir conteúdo de domínio — Req 1.4)
        var structure = await _parser.ParseStructureAsync(request.FileStream, cancellationToken);

        // 4. Valida colunas — MIG-ERR-002
        SpreadsheetStructureValidator.Validate(structure);

        // 5. Cria MigrationJob em Created
        var tenantId = _tenantContext.TenantId
            ?? throw new MigrationDomainException("MIG-ERR-005", "Tenant não identificado no contexto.");

        var createdBy = _tenantContext.UserId
            ?? throw new MigrationDomainException("MIG-ERR-005", "Usuário não identificado no contexto.");

        var job = MigrationJob.Create(
            tenantId: tenantId,
            sourceFileName: request.FileName,
            sourceFileSizeBytes: request.FileSizeBytes,
            sourceFileHash: request.FileHash,
            detectedRowCount: structure.DetectedRowCount,
            createdBy: createdBy,
            createdAt: _clock.UtcNow);

        // 6. Persiste o job
        await _repository.AddAsync(job, cancellationToken);

        return new UploadSpreadsheetResult(
            JobId: job.Id,
            Status: "created",
            DetectedRowCount: job.DetectedRowCount);
    }
}
