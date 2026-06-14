using DataMigration.Application.Commands.DryRun;
using DataMigration.Application.Commands.Import;
using DataMigration.Application.Commands.Triage;
using DataMigration.Application.Commands.Upload;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Queries;
using DataMigration.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataMigration.Api.Controllers;

/// <summary>
/// Controller REST do módulo data-migration.
///
/// Endpoints (design §8):
///   POST   /api/v1/migrations/upload                        — PlatOp
///   POST   /api/v1/migrations/{jobId}/dry-run               — PlatOp
///   GET    /api/v1/migrations/{jobId}/status                — PlatOp, TenantAdmin
///   GET    /api/v1/migrations/{jobId}/triage                — PlatOp, TenantAdmin
///   POST   /api/v1/migrations/{jobId}/triage/owners         — TenantAdmin, PlatOp
///   POST   /api/v1/migrations/{jobId}/triage/owners/bulk    — TenantAdmin, PlatOp
///   POST   /api/v1/migrations/{jobId}/triage/stages         — TenantAdmin, PlatOp
///   POST   /api/v1/migrations/{jobId}/triage/partners       — TenantAdmin, PlatOp
///   POST   /api/v1/migrations/{jobId}/triage/dedupe         — TenantAdmin, PlatOp
///   POST   /api/v1/migrations/{jobId}/ready                 — TenantAdmin, PlatOp
///   POST   /api/v1/migrations/{jobId}/execute               — PlatOp
///   GET    /api/v1/migrations/{jobId}/report                — TenantAdmin, PlatOp
///
/// Autorização: JWT via <see cref="AuthorizationBehavior{TRequest,TResponse}"/> no pipeline MediatR.
/// Erros: ProblemDetails com catálogo MIG-ERR-001..010 (design §12).
///
/// Rastreia: design §8, §10, §12, TASK-21, TASK-22, TASK-23.
/// </summary>
[ApiController]
[Route("api/v1/migrations")]
[Authorize]
public sealed class MigrationController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>Cria o controller com o mediador injetado.</summary>
    public MigrationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // =========================================================================
    // TASK-21 — Upload, Dry-Run e Status
    // =========================================================================

    /// <summary>
    /// Upload do arquivo .xlsx. Cria <c>MigrationJob</c> em estado <c>created</c>.
    ///
    /// Autorização: Platform Operator.
    /// Erros: MIG-ERR-001 (não-.xlsx), MIG-ERR-002 (colunas ausentes), MIG-ERR-003 (tamanho).
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB (design §15)
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413RequestEntityTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(
                detail: "Arquivo não fornecido ou vazio.",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "MIG-ERR-001");
        }

        var fileHash = await ComputeSha256Async(file, cancellationToken);

        var command = new UploadSpreadsheetCommand(
            FileName: file.FileName,
            FileStream: file.OpenReadStream(),
            FileSizeBytes: file.Length,
            FileHash: fileHash);

        var result = await _mediator.Send(command, cancellationToken);

        return Ok(new
        {
            jobId = result.JobId,
            status = result.Status,
            detectedRowCount = result.DetectedRowCount,
        });
    }

    /// <summary>
    /// Executa o dry-run para o job especificado.
    /// Retorna o relatório de triagem sem persistir dados de domínio.
    ///
    /// Autorização: Platform Operator.
    /// Erros: MIG-ERR-004 (job não encontrado), MIG-ERR-005 (estado inválido).
    /// </summary>
    [HttpPost("{jobId:guid}/dry-run")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DryRun(
        Guid jobId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        Stream fileStream = file is not null
            ? file.OpenReadStream()
            : Stream.Null;

        var command = new RunDryRunCommand(jobId, fileStream);
        var result = await _mediator.Send(command, cancellationToken);

        return Ok(new
        {
            jobId = result.JobId,
            status = result.Status,
            report = result.Report,
        });
    }

    /// <summary>
    /// Retorna o status atual do job: estado, contagens, progresso.
    ///
    /// Autorização: Platform Operator ou Tenant Admin.
    /// Erros: MIG-ERR-004 (job não encontrado).
    /// </summary>
    [HttpGet("{jobId:guid}/status")]
    [ProducesResponseType(typeof(MigrationJobStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Status(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var query = new GetMigrationJobStatusQuery(jobId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    // =========================================================================
    // TASK-22 — Endpoints de Triagem
    // =========================================================================

    /// <summary>
    /// Retorna o relatório de triagem com flags e divergências.
    /// Suporta paginação por tipo de flag (<c>?flag=owner_missing&amp;page=1&amp;pageSize=50</c>).
    ///
    /// Autorização: Platform Operator ou Tenant Admin.
    /// Erros: MIG-ERR-004 (job não encontrado).
    /// </summary>
    [HttpGet("{jobId:guid}/triage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTriage(
        Guid jobId,
        [FromQuery] string? flag = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetTriageReportQuery(jobId, flag, page, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Atribuição individual de owner a uma oportunidade triada.
    ///
    /// Autorização: Tenant Admin (ou Platform Operator).
    /// Erros: MIG-ERR-004.
    /// </summary>
    [HttpPost("{jobId:guid}/triage/owners")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignOwner(
        Guid jobId,
        [FromBody] AssignOwnerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AssignOwnerCommand(jobId, request.RowIndex, request.OwnerId);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Atribuição em massa de owner a todas as oportunidades de uma BU.
    ///
    /// Autorização: Tenant Admin (ou Platform Operator).
    /// Erros: MIG-ERR-004.
    /// </summary>
    [HttpPost("{jobId:guid}/triage/owners/bulk")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkAssignOwner(
        Guid jobId,
        [FromBody] BulkAssignOwnerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new BulkAssignOwnerCommand(
            jobId, request.BuName, request.OwnerId, request.RowIndexes);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Resolução de estágio faltante para uma linha.
    ///
    /// Autorização: Tenant Admin (ou Platform Operator).
    /// Erros: MIG-ERR-004.
    /// </summary>
    [HttpPost("{jobId:guid}/triage/stages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveStage(
        Guid jobId,
        [FromBody] ResolveStageRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ResolveStagePendingCommand(jobId, request.RowIndex, request.StageId);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Resolução de parceiro/percentual pendente para uma linha.
    ///
    /// Autorização: Tenant Admin (ou Platform Operator).
    /// Erros: MIG-ERR-004.
    /// </summary>
    [HttpPost("{jobId:guid}/triage/partners")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolvePartner(
        Guid jobId,
        [FromBody] ResolvePartnerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ResolvePartnerPctCommand(jobId, request.RowIndex, request.PartnerId);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Resolução de par candidato a dedupe (merge ou manter ambas as contas).
    ///
    /// Autorização: Tenant Admin (ou Platform Operator).
    /// Erros: MIG-ERR-004.
    /// </summary>
    [HttpPost("{jobId:guid}/triage/dedupe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveDedupe(
        Guid jobId,
        [FromBody] ResolveDedupeRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ResolveDedupeCommand(
            jobId, request.RowIndexA, request.RowIndexB, request.MergeIntoRowA);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Marca o job como pronto para import.
    /// Rejeita com MIG-ERR-006 se houver oportunidades sem owner.
    ///
    /// Autorização: Tenant Admin (ou Platform Operator).
    /// Erros: MIG-ERR-004, MIG-ERR-006.
    /// </summary>
    [HttpPost("{jobId:guid}/ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkReady(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var command = new MarkReadyToImportCommand(jobId);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    // =========================================================================
    // TASK-23 — Execute e Report
    // =========================================================================

    /// <summary>
    /// Executa o import transacional tudo-ou-nada.
    /// Requer <c>confirmation: true</c> (Req 6.2) e feature flag habilitada (DD-009).
    ///
    /// Autorização: Platform Operator.
    /// Erros: MIG-ERR-004..010.
    /// </summary>
    [HttpPost("{jobId:guid}/execute")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Execute(
        Guid jobId,
        [FromBody] ExecuteImportRequest request,
        CancellationToken cancellationToken)
    {
        // Validação de confirmação explícita na borda da API (Req 6.2, MIG-ERR-008)
        if (!request.Confirmation)
        {
            return Problem(
                detail: "Confirmação explícita obrigatória para executar o import. Envie 'confirmation: true'.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "MIG-ERR-008");
        }

        var command = new ExecuteImportCommand(
            JobId: jobId,
            Confirmation: request.Confirmation,
            FileStream: Stream.Null);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.Status == "completed" && result.Report is not null)
        {
            return Ok(new
            {
                jobId = result.JobId,
                status = result.Status,
                counts = result.Report.Counts,
                flagsResolved = result.Report.FlagsResolved,
                forecastDivergences = result.Report.ForecastDivergences,
                freezeInstruction = result.Report.FreezeInstruction,
            });
        }

        return Ok(new
        {
            jobId = result.JobId,
            status = result.Status,
        });
    }

    /// <summary>
    /// Retorna o relatório final auditado do import.
    /// Disponível apenas quando o job está em estado <c>completed</c>.
    ///
    /// Autorização: Tenant Admin (ou Platform Operator).
    /// Erros: MIG-ERR-004 (job não encontrado ou não completed).
    /// </summary>
    [HttpGet("{jobId:guid}/report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReport(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var query = new GetImportReportQuery(jobId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static async Task<string> ComputeSha256Async(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        using var stream = file.OpenReadStream();
        var hashBytes = await System.Security.Cryptography.SHA256.HashDataAsync(
            stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

// =========================================================================
// Request DTOs (contratos de entrada)
// =========================================================================

/// <summary>Request body para atribuição individual de owner.</summary>
public sealed record AssignOwnerRequest(int RowIndex, Guid OwnerId);

/// <summary>Request body para atribuição em massa de owner por BU.</summary>
public sealed record BulkAssignOwnerRequest(
    string BuName,
    Guid OwnerId,
    IReadOnlyList<int> RowIndexes);

/// <summary>Request body para resolução de estágio.</summary>
public sealed record ResolveStageRequest(int RowIndex, Guid StageId);

/// <summary>Request body para resolução de parceiro.</summary>
public sealed record ResolvePartnerRequest(int RowIndex, Guid? PartnerId);

/// <summary>Request body para resolução de dedupe.</summary>
public sealed record ResolveDedupeRequest(int RowIndexA, int RowIndexB, bool MergeIntoRowA);

/// <summary>
/// Request body para execute do import.
/// <c>Confirmation</c> deve ser <c>true</c> (MIG-ERR-008, Req 6.2).
/// </summary>
public sealed record ExecuteImportRequest(
    bool Confirmation,
    string? IdempotencyKey = null);
