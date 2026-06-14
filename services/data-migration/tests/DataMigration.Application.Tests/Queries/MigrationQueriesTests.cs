using System.Text.Json;
using DataMigration.Application.DTOs;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using DataMigration.Application.Queries;
using DataMigration.Domain.Aggregates;
using DataMigration.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DataMigration.Application.Tests.Queries;

/// <summary>
/// Testes dos handlers de query.
///
/// Rastreia: TASK-13, design §5.2, Req 2, Req 11, RNF 3.
/// </summary>
public sealed class MigrationQueriesTests
{
    private readonly IMigrationJobRepository _repository;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 6, 14, 18, 0, 0, TimeSpan.Zero);

    public MigrationQueriesTests()
    {
        _repository = Substitute.For<IMigrationJobRepository>();
    }

    private MigrationJob BuildCompletedJob()
    {
        var job = MigrationJob.Create(
            tenantId: _tenantId,
            sourceFileName: "pipeline.xlsx",
            sourceFileSizeBytes: 1024L,
            sourceFileHash: "sha256-abc",
            detectedRowCount: 108,
            createdBy: _userId,
            createdAt: _now.AddHours(-3));

        job.TransitionTo(MigrationJobStatus.DryRunCompleted, _now.AddHours(-2));

        var triageReport = new TriageReportDto
        {
            TotalOpportunities = 108,
            TotalAccounts = 71,
            OwnerMissingCount = 0,
            Flags = [TriageFlag.ForRow(TriageFlagType.StageMissing, 5)],
        };
        job.SetTriageReport(JsonSerializer.Serialize(triageReport), _now.AddHours(-2));

        job.TransitionTo(MigrationJobStatus.TriageInProgress, _now.AddMinutes(-90));
        job.TransitionTo(MigrationJobStatus.ReadyToImport, _now.AddMinutes(-60));
        job.TransitionToImporting(ownerlessCandidateCount: 0, at: _now.AddMinutes(-30));

        var importReport = new ImportReportDto
        {
            JobId = job.Id,
            Counts = new ImportEntityCounts(71, 58, 9, 108, 42),
            FlagsResolved = 5,
            ForecastDivergences = 3,
            CompletedAt = _now,
            SourceFileHash = "sha256-abc",
        };
        job.SetImportReport(JsonSerializer.Serialize(importReport), _now);
        job.TransitionTo(MigrationJobStatus.Completed, _now);

        return job;
    }

    private MigrationJob BuildDryRunCompletedJob()
    {
        var job = MigrationJob.Create(
            tenantId: _tenantId,
            sourceFileName: "pipeline.xlsx",
            sourceFileSizeBytes: 1024L,
            sourceFileHash: "sha256-xyz",
            detectedRowCount: 50,
            createdBy: _userId,
            createdAt: _now.AddHours(-1));

        job.TransitionTo(MigrationJobStatus.DryRunCompleted, _now.AddMinutes(-30));

        var triageReport = new TriageReportDto
        {
            TotalOpportunities = 50,
            TotalAccounts = 30,
            OwnerMissingCount = 10,
            Flags =
            [
                TriageFlag.ForRow(TriageFlagType.OwnerMissing, 0),
                TriageFlag.ForRow(TriageFlagType.OwnerMissing, 1),
                TriageFlag.ForRow(TriageFlagType.StageMissing, 3),
                TriageFlag.ForRow(TriageFlagType.PartnerPctMissing, 5),
                TriageFlag.ForRow(TriageFlagType.DedupeCandidate, 7),
            ],
            ForecastDivergences = [new ForecastDivergenceEntry(2, 100000L, 98000L, -2000L)],
        };
        job.SetTriageReport(JsonSerializer.Serialize(triageReport), _now.AddMinutes(-30));

        return job;
    }

    // =========================================================================
    // GetMigrationJobStatusQuery
    // =========================================================================

    [Fact(DisplayName = "GetMigrationJobStatus retorna estado correto e contagens")]
    public async Task GetMigrationJobStatus_ReturnsCorrectStateAndCounts()
    {
        // Arrange
        var job = BuildDryRunCompletedJob();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new GetMigrationJobStatusHandler(_repository);
        var query = new GetMigrationJobStatusQuery(job.Id);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.JobId.Should().Be(job.Id);
        result.Status.Should().Be("dry_run_completed");
        result.DetectedRowCount.Should().Be(50);
        result.SourceFileName.Should().Be("pipeline.xlsx");
    }

    [Fact(DisplayName = "GetMigrationJobStatus lança MIG-ERR-004 para job não encontrado")]
    public async Task GetMigrationJobStatus_WhenNotFound_ThrowsMigErr004()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _repository.GetByIdAsync(jobId, Arg.Any<CancellationToken>()).Returns((MigrationJob?)null);

        var handler = new GetMigrationJobStatusHandler(_repository);
        var query = new GetMigrationJobStatusQuery(jobId);

        // Act
        var act = () => handler.Handle(query, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<MigrationDomainException>();
        ex.Which.ErrorCode.Should().Be("MIG-ERR-004");
    }

    // =========================================================================
    // GetTriageReportQuery
    // =========================================================================

    [Fact(DisplayName = "GetTriageReport retorna flags, dedupe e divergências")]
    public async Task GetTriageReport_ReturnsFlagsDedupeAndDivergences()
    {
        // Arrange
        var job = BuildDryRunCompletedJob();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new GetTriageReportHandler(_repository);
        var query = new GetTriageReportQuery(job.Id, FlagType: null, Page: 1, PageSize: 50);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalOpportunities.Should().Be(50);
        result.OwnerMissingCount.Should().Be(10);
        result.Flags.Should().NotBeEmpty();
        result.ForecastDivergences.Should().HaveCount(1);
    }

    [Fact(DisplayName = "GetTriageReport pagina flags por tipo corretamente")]
    public async Task GetTriageReport_PaginatesFlagsByType()
    {
        // Arrange
        var job = BuildDryRunCompletedJob();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new GetTriageReportHandler(_repository);
        var query = new GetTriageReportQuery(job.Id, FlagType: "owner_missing", Page: 1, PageSize: 50);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert: apenas flags do tipo OwnerMissing
        result.Flags.Should().AllSatisfy(f =>
            f.FlagType.Should().Be(TriageFlagType.OwnerMissing));
    }

    // =========================================================================
    // GetImportReportQuery — disponível apenas em Completed
    // =========================================================================

    [Fact(DisplayName = "GetImportReport disponível apenas após completed")]
    public async Task GetImportReport_AvailableOnlyWhenCompleted()
    {
        // Arrange
        var job = BuildCompletedJob();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new GetImportReportHandler(_repository);
        var query = new GetImportReportQuery(job.Id);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Counts.Accounts.Should().Be(71);
        result.Counts.Opportunities.Should().Be(108);
        result.FlagsResolved.Should().Be(5);
        result.ForecastDivergences.Should().Be(3);
        result.FreezeInstruction.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "GetImportReport lança erro quando job não está em Completed")]
    public async Task GetImportReport_WhenNotCompleted_ThrowsError()
    {
        // Arrange
        var job = BuildDryRunCompletedJob();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new GetImportReportHandler(_repository);
        var query = new GetImportReportQuery(job.Id);

        // Act
        var act = () => handler.Handle(query, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<MigrationDomainException>();
        ex.Which.ErrorCode.Should().Be("MIG-ERR-004");
    }

    // =========================================================================
    // Zero PII em DTOs
    // =========================================================================

    [Fact(DisplayName = "GetMigrationJobStatus não expõe PII nos DTOs")]
    public async Task GetMigrationJobStatus_DoesNotExposePii()
    {
        // Arrange
        var job = BuildCompletedJob();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new GetMigrationJobStatusHandler(_repository);
        var result = await handler.Handle(new GetMigrationJobStatusQuery(job.Id), CancellationToken.None);

        // Serializa o DTO e verifica ausência de padrões de PII
        var json = JsonSerializer.Serialize(result);
        json.Should().NotContain("@"); // sem e-mails
        json.Should().NotMatchRegex(@"\d{3}\.?\d{3}\.?\d{3}-?\d{2}"); // sem CPF
    }
}
