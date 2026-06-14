using DataMigration.Application.Commands.DryRun;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using DataMigration.Domain.Aggregates;
using DataMigration.Domain.ValueObjects;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using NSubstitute;
using Xunit;

namespace DataMigration.Application.Tests.Commands;

/// <summary>
/// Testes do handler <see cref="RunDryRunHandler"/>.
///
/// Rastreia: TASK-09, design §5.3, Req 2, PBT-04.
/// </summary>
public sealed class RunDryRunHandlerTests
{
    private readonly ISpreadsheetParser _parser;
    private readonly IMigrationJobRepository _repository;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly RunDryRunHandler _handler;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

    public RunDryRunHandlerTests()
    {
        _parser = Substitute.For<ISpreadsheetParser>();
        _repository = Substitute.For<IMigrationJobRepository>();
        _uow = Substitute.For<IUnitOfWork>();
        _clock = Substitute.For<IClock>();

        _clock.UtcNow.Returns(_now);

        _handler = new RunDryRunHandler(_parser, _repository, _uow, _clock);
    }

    private MigrationJob BuildCreatedJob() =>
        MigrationJob.Create(
            tenantId: _tenantId,
            sourceFileName: "pipeline.xlsx",
            sourceFileSizeBytes: 1024L,
            sourceFileHash: "sha256-test",
            detectedRowCount: 3,
            createdBy: _userId,
            createdAt: _now.AddMinutes(-5));

    private static IReadOnlyList<SourceRow> BuildPipelineRows(int count, string accountPattern = "Conta {0}")
    {
        var rows = new List<SourceRow>();
        for (int i = 0; i < count; i++)
        {
            rows.Add(new SourceRow("pipeline", i, new Dictionary<string, string?>
            {
                ["BU"] = "Sertão",
                ["Conta"] = string.Format(accountPattern, i),
                ["Título"] = $"Oportunidade {i}",
                ["Responsável"] = string.Empty,
                ["Estágio"] = string.Empty,
                ["Parceiro"] = "-",
                ["Valor Setup"] = "100000",
                ["Valor Mensal"] = "50000",
                ["Meses"] = "12",
                ["Forecast (R$)"] = "60000",
                ["Nº Oportunidade"] = string.Empty,
                ["Data de Fechamento"] = string.Empty,
            }));
        }

        return rows;
    }

    private static IReadOnlyList<SourceRow> BuildRowsWithActivities(int pipeline, int acoes)
    {
        var rows = BuildPipelineRows(pipeline).ToList();
        for (int i = 0; i < acoes; i++)
        {
            rows.Add(new SourceRow("acoes_comerciais", i, new Dictionary<string, string?>
            {
                ["Ação"] = "Reunião",
                ["Data"] = "45000",
                ["Responsável"] = "Milton",
                ["Nº Oportunidade"] = "AZ-0001",
            }));
        }

        return rows.AsReadOnly();
    }

    // =========================================================================
    // ST-01 (a) — Transação rollback-only, zero writes de domínio
    // =========================================================================

    [Fact(DisplayName = "Dry-run usa transação rollback-only; commit nunca chamado")]
    public async Task Handle_AlwaysUsesRollbackOnlyTransaction()
    {
        // Arrange
        var job = BuildCreatedJob();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _parser.ParseRowsAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(BuildPipelineRows(3));

        var command = new RunDryRunCommand(job.Id, Stream.Null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _uow.Received(1).BeginRollbackOnlyAsync(Arg.Any<CancellationToken>());
        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Job não encontrado lança MIG-ERR-004")]
    public async Task Handle_WhenJobNotFound_ThrowsMigErr004()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _repository.GetByIdAsync(jobId, Arg.Any<CancellationToken>()).Returns((MigrationJob?)null);

        var command = new RunDryRunCommand(jobId, Stream.Null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<MigrationDomainException>();
        ex.Which.ErrorCode.Should().Be("MIG-ERR-004");
    }

    // =========================================================================
    // ST-01 (b) — TriageReport correto
    // =========================================================================

    [Fact(DisplayName = "TriageReport contém contagem de oportunidades e flags corretos")]
    public async Task Handle_ReturnsTriageReportWithCorrectCounts()
    {
        // Arrange
        var job = BuildCreatedJob();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _parser.ParseRowsAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(BuildRowsWithActivities(5, 2));

        var command = new RunDryRunCommand(job.Id, Stream.Null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Report.TotalOpportunities.Should().Be(5);
        result.Report.TotalActivities.Should().Be(2);
        result.Report.OwnerMissingCount.Should().Be(5); // todas sem responsável
        result.Report.StageMissingCount.Should().Be(5); // todas sem estágio
    }

    // =========================================================================
    // ST-01 (c) — Job transita para dry_run_completed
    // =========================================================================

    [Fact(DisplayName = "Job transita para DryRunCompleted após dry-run bem-sucedido")]
    public async Task Handle_TransitionsJobToDryRunCompleted()
    {
        // Arrange
        var job = BuildCreatedJob();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _parser.ParseRowsAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(BuildPipelineRows(2));

        var command = new RunDryRunCommand(job.Id, Stream.Null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        job.Status.Should().Be(MigrationJobStatus.DryRunCompleted);
        job.TriageReportJson.Should().NotBeNullOrEmpty();
        await _repository.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // PBT-04 — Conservação de contagem (oportunidades = linhas pipeline)
    // =========================================================================

    /// <summary>
    /// PBT-04: para qualquer conjunto de linhas pipeline, o número de oportunidades
    /// no relatório = número de linhas; número de contas = NormalizedNames distintos.
    ///
    /// Rastreia: PBT-04, design §13, Req 2.
    /// </summary>
    [Property(MaxTest = 50, DisplayName = "PBT-04: oportunidades = linhas pipeline")]
    public Property Pbt04_OpportunityCountEqualsValidPipelineRows(PositiveInt n)
    {
        var rowCount = Math.Min(n.Get, 30);

        var rows = BuildPipelineRows(rowCount);

        var parser = Substitute.For<ISpreadsheetParser>();
        var repository = Substitute.For<IMigrationJobRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(_now);

        var job = MigrationJob.Create(
            tenantId: _tenantId,
            sourceFileName: "test.xlsx",
            sourceFileSizeBytes: 1024L,
            sourceFileHash: "hash",
            detectedRowCount: rowCount,
            createdBy: _userId,
            createdAt: _now);

        repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        parser.ParseRowsAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(rows);

        var handler = new RunDryRunHandler(parser, repository, uow, clock);
        var result = handler.Handle(
            new RunDryRunCommand(job.Id, Stream.Null),
            CancellationToken.None).GetAwaiter().GetResult();

        return (result.Report.TotalOpportunities == rowCount)
            .ToProperty()
            .Label($"TotalOpportunities={result.Report.TotalOpportunities}, rowCount={rowCount}");
    }

    [Property(MaxTest = 50, DisplayName = "PBT-04: contas = NormalizedNames distintos")]
    public Property Pbt04_AccountCountEqualsDistinctNormalizedNames(PositiveInt n)
    {
        var rowCount = Math.Min(n.Get, 20);

        // Cria linhas com nomes de conta que têm duplicatas (modulo 4 contas distintas)
        var rows = new List<SourceRow>();
        for (int i = 0; i < rowCount; i++)
        {
            rows.Add(new SourceRow("pipeline", i, new Dictionary<string, string?>
            {
                ["BU"] = "TI",
                ["Conta"] = $"Empresa {i % 4}",  // máx 4 contas distintas
                ["Título"] = $"Op {i}",
                ["Responsável"] = string.Empty,
                ["Estágio"] = string.Empty,
                ["Parceiro"] = "-",
                ["Valor Setup"] = "0",
                ["Valor Mensal"] = "0",
                ["Meses"] = "0",
                ["Forecast (R$)"] = "0",
                ["Nº Oportunidade"] = string.Empty,
                ["Data de Fechamento"] = string.Empty,
            }));
        }

        var expectedDistinct = rows
            .Select(r => r.Cells["Conta"]?.Trim().ToLowerInvariant())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .Count();

        var parser = Substitute.For<ISpreadsheetParser>();
        var repository = Substitute.For<IMigrationJobRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(_now);

        var job = MigrationJob.Create(
            tenantId: _tenantId,
            sourceFileName: "test.xlsx",
            sourceFileSizeBytes: 1024L,
            sourceFileHash: "hash",
            detectedRowCount: rowCount,
            createdBy: _userId,
            createdAt: _now);

        repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        parser.ParseRowsAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SourceRow>)rows);

        var handler = new RunDryRunHandler(parser, repository, uow, clock);
        var result = handler.Handle(
            new RunDryRunCommand(job.Id, Stream.Null),
            CancellationToken.None).GetAwaiter().GetResult();

        return (result.Report.TotalAccounts == expectedDistinct)
            .ToProperty()
            .Label($"TotalAccounts={result.Report.TotalAccounts}, expected={expectedDistinct}");
    }
}
