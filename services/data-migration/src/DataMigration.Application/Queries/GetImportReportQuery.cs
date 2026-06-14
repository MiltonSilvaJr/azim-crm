using System.Text.Json;
using DataMigration.Application.DTOs;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using DataMigration.Domain.Aggregates;
using MediatR;

namespace DataMigration.Application.Queries;

/// <summary>
/// Query de relatório final do import.
/// Disponível apenas quando o job está em <c>Completed</c>.
///
/// Rastreia: design §5.2, Req 11, Req 13, TASK-13.
/// </summary>
/// <param name="JobId">ID do job.</param>
public sealed record GetImportReportQuery(Guid JobId)
    : IRequest<ImportReportDto>;

/// <summary>
/// Handler da query de relatório final.
///
/// Retorna erro (MIG-ERR-004) quando job não está em <c>Completed</c>
/// (design §5.3 — relatório disponível apenas após import bem-sucedido).
///
/// Rastreia: design §5.2, Req 11, RNF 3, TASK-13.
/// </summary>
public sealed class GetImportReportHandler
    : IRequestHandler<GetImportReportQuery, ImportReportDto>
{
    private readonly IMigrationJobRepository _repository;

    /// <summary>Cria o handler com o repositório injetado.</summary>
    public GetImportReportHandler(IMigrationJobRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<ImportReportDto> Handle(
        GetImportReportQuery request,
        CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new MigrationDomainException("MIG-ERR-004",
                $"Job de migração não encontrado: {request.JobId}.");

        if (job.Status != MigrationJobStatus.Completed)
        {
            throw new MigrationDomainException(
                "MIG-ERR-004",
                $"Relatório de import disponível apenas após conclusão bem-sucedida. " +
                $"Estado atual: '{job.Status}'.");
        }

        if (string.IsNullOrWhiteSpace(job.ImportReportJson))
        {
            throw new MigrationDomainException(
                "MIG-ERR-004",
                "Relatório de import não encontrado no job.");
        }

        try
        {
            var report = JsonSerializer.Deserialize<ImportReportDto>(job.ImportReportJson)
                ?? throw new MigrationDomainException("MIG-ERR-004", "Relatório de import inválido.");

            return report;
        }
        catch (JsonException ex)
        {
            throw new MigrationDomainException(
                "MIG-ERR-004",
                "Relatório de import não pode ser lido.", ex);
        }
    }
}
