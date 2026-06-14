using System.Text.Json;
using DataMigration.Application.DTOs;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using DataMigration.Domain.ValueObjects;
using MediatR;

namespace DataMigration.Application.Queries;

/// <summary>
/// Query de relatório de triagem com filtro e paginação de flags.
///
/// Rastreia: design §5.2, §8, Req 2, TASK-13.
/// </summary>
/// <param name="JobId">ID do job.</param>
/// <param name="FlagType">Tipo de flag para filtro (ex: "owner_missing"). Null = todos.</param>
/// <param name="Page">Página (base-1).</param>
/// <param name="PageSize">Itens por página.</param>
public sealed record GetTriageReportQuery(
    Guid JobId,
    string? FlagType,
    int Page,
    int PageSize) : IRequest<TriageReportDto>;

/// <summary>
/// Handler da query de relatório de triagem.
///
/// Suporta paginação de pendências por tipo de flag
/// (<c>?flag=owner_missing&amp;page=1&amp;pageSize=50</c> — design §8).
///
/// Rastreia: design §5.2, §8, Req 2, RNF 3, TASK-13.
/// </summary>
public sealed class GetTriageReportHandler
    : IRequestHandler<GetTriageReportQuery, TriageReportDto>
{
    private readonly IMigrationJobRepository _repository;

    /// <summary>Cria o handler com o repositório injetado.</summary>
    public GetTriageReportHandler(IMigrationJobRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<TriageReportDto> Handle(
        GetTriageReportQuery request,
        CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new MigrationDomainException("MIG-ERR-004",
                $"Job de migração não encontrado: {request.JobId}.");

        if (string.IsNullOrWhiteSpace(job.TriageReportJson))
        {
            // Job ainda não passou pelo dry-run
            return new TriageReportDto();
        }

        TriageReportDto report;
        try
        {
            report = JsonSerializer.Deserialize<TriageReportDto>(job.TriageReportJson)
                ?? new TriageReportDto();
        }
        catch (JsonException)
        {
            return new TriageReportDto();
        }

        // Paginação de flags por tipo (design §8)
        if (!string.IsNullOrWhiteSpace(request.FlagType))
        {
            var flagTypeEnum = ParseFlagType(request.FlagType);
            if (flagTypeEnum.HasValue)
            {
                var filteredFlags = report.Flags
                    .Where(f => f.FlagType == flagTypeEnum.Value)
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList()
                    .AsReadOnly();

                return new TriageReportDto
                {
                    TotalOpportunities = report.TotalOpportunities,
                    TotalAccounts = report.TotalAccounts,
                    TotalActivities = report.TotalActivities,
                    ByBu = report.ByBu,
                    OwnerMissingCount = report.OwnerMissingCount,
                    StageMissingCount = report.StageMissingCount,
                    PartnerPctMissingCount = report.PartnerPctMissingCount,
                    DedupeCandidateCount = report.DedupeCandidateCount,
                    TypoCount = report.TypoCount,
                    Flags = filteredFlags,
                    DedupeCandidates = report.DedupeCandidates,
                    ForecastDivergences = report.ForecastDivergences,
                };
            }
        }

        return report;
    }

    private static TriageFlagType? ParseFlagType(string flagType) =>
        flagType.ToLowerInvariant() switch
        {
            "owner_missing" => TriageFlagType.OwnerMissing,
            "stage_missing" => TriageFlagType.StageMissing,
            "partner_pct_missing" => TriageFlagType.PartnerPctMissing,
            "dedupe_candidate" => TriageFlagType.DedupeCandidate,
            "bu_unknown" => TriageFlagType.BuUnknown,
            "typo" => TriageFlagType.Typo,
            _ => null,
        };
}
