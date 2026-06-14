using System.Text.Json;
using DataMigration.Application.DTOs;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using DataMigration.Domain.Aggregates;
using MediatR;

namespace DataMigration.Application.Queries;

/// <summary>
/// DTO de status do job. Sem PII (RNF 3).
/// </summary>
public sealed class MigrationJobStatusDto
{
    /// <summary>ID do job.</summary>
    public Guid JobId { get; init; }

    /// <summary>Estado atual (snake_case).</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Nome do arquivo de origem.</summary>
    public string SourceFileName { get; init; } = string.Empty;

    /// <summary>Hash SHA-256 do arquivo (auditoria).</summary>
    public string SourceFileHash { get; init; } = string.Empty;

    /// <summary>Número de linhas detectadas.</summary>
    public int DetectedRowCount { get; init; }

    /// <summary>Instante de criação do job (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Instante de última atualização (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Instante de início do import (nulo antes de Importing).</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Instante de término do import (nulo antes de Completed/RolledBack).</summary>
    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>Resumo do relatório de triagem (contagens de flags).</summary>
    public TriageReportSummary? TriageReport { get; init; }
}

/// <summary>
/// Resumo do relatório de triagem para o status do job.
/// </summary>
public sealed class TriageReportSummary
{
    /// <summary>Total de oportunidades.</summary>
    public int TotalOpportunities { get; init; }

    /// <summary>Total de contas distintas.</summary>
    public int TotalAccounts { get; init; }

    /// <summary>Oportunidades sem owner (bloqueante).</summary>
    public int OwnerMissingCount { get; init; }

    /// <summary>Total de flags de triagem.</summary>
    public int TotalFlags { get; init; }
}

/// <summary>
/// Query de status do job de migração.
///
/// Rastreia: design §5.2, Req 11, TASK-13.
/// </summary>
/// <param name="JobId">ID do job.</param>
public sealed record GetMigrationJobStatusQuery(Guid JobId)
    : IRequest<MigrationJobStatusDto>;

/// <summary>
/// Handler da query de status do job.
///
/// Rastreia: design §5.2, Req 11, RNF 3, TASK-13.
/// </summary>
public sealed class GetMigrationJobStatusHandler
    : IRequestHandler<GetMigrationJobStatusQuery, MigrationJobStatusDto>
{
    private readonly IMigrationJobRepository _repository;

    /// <summary>Cria o handler com o repositório injetado.</summary>
    public GetMigrationJobStatusHandler(IMigrationJobRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<MigrationJobStatusDto> Handle(
        GetMigrationJobStatusQuery request,
        CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new MigrationDomainException("MIG-ERR-004",
                $"Job de migração não encontrado: {request.JobId}.");

        TriageReportSummary? triageSummary = null;
        if (!string.IsNullOrWhiteSpace(job.TriageReportJson))
        {
            try
            {
                var report = JsonSerializer.Deserialize<TriageReportDto>(job.TriageReportJson);
                if (report is not null)
                {
                    triageSummary = new TriageReportSummary
                    {
                        TotalOpportunities = report.TotalOpportunities,
                        TotalAccounts = report.TotalAccounts,
                        OwnerMissingCount = report.OwnerMissingCount,
                        TotalFlags = report.Flags.Count,
                    };
                }
            }
            catch (JsonException)
            {
                // JSON malformado: triageSummary permanece null
            }
        }

        return new MigrationJobStatusDto
        {
            JobId = job.Id,
            Status = ToSnakeCase(job.Status),
            SourceFileName = job.SourceFileName,
            SourceFileHash = job.SourceFileHash,
            DetectedRowCount = job.DetectedRowCount,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt,
            StartedAt = job.StartedAt,
            FinishedAt = job.FinishedAt,
            TriageReport = triageSummary,
        };
    }

    private static string ToSnakeCase(MigrationJobStatus status) => status switch
    {
        MigrationJobStatus.Created => "created",
        MigrationJobStatus.DryRunCompleted => "dry_run_completed",
        MigrationJobStatus.TriageInProgress => "triage_in_progress",
        MigrationJobStatus.ReadyToImport => "ready_to_import",
        MigrationJobStatus.Importing => "importing",
        MigrationJobStatus.Completed => "completed",
        MigrationJobStatus.RolledBack => "rolled_back",
        MigrationJobStatus.Failed => "failed",
        _ => status.ToString().ToLowerInvariant(),
    };
}
