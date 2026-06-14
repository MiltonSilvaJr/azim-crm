using DataMigration.Application.Commands.Upload;
using DataMigration.Application.DTOs;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using DataMigration.Domain.Aggregates;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DataMigration.Application.Tests.Commands;

/// <summary>
/// Testes do handler <see cref="UploadSpreadsheetHandler"/>.
///
/// Rastreia: TASK-08, design §5.3, Req 1; MIG-ERR-001, MIG-ERR-002, MIG-ERR-003.
/// </summary>
public sealed class UploadSpreadsheetHandlerTests
{
    private readonly ISpreadsheetParser _parser;
    private readonly IMigrationJobRepository _repository;
    private readonly IClock _clock;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly UploadSpreadsheetHandler _handler;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 6, 14, 10, 0, 0, TimeSpan.Zero);

    public UploadSpreadsheetHandlerTests()
    {
        _parser = Substitute.For<ISpreadsheetParser>();
        _repository = Substitute.For<IMigrationJobRepository>();
        _clock = Substitute.For<IClock>();
        _tenantContext = Substitute.For<ICurrentTenantContext>();

        _clock.UtcNow.Returns(_now);
        _tenantContext.TenantId.Returns(_tenantId);
        _tenantContext.UserId.Returns(_userId);

        _handler = new UploadSpreadsheetHandler(_parser, _repository, _clock, _tenantContext);
    }

    // =========================================================================
    // ST-01 — Casos de rejeição (sem escritas)
    // =========================================================================

    [Fact(DisplayName = "MIG-ERR-001: arquivo não-.xlsx rejeitado sem escrita")]
    public async Task Handle_WhenFileIsNotXlsx_ThrowsMigErrWithoutWrite()
    {
        // Arrange
        var command = new UploadSpreadsheetCommand(
            FileName: "pipeline.csv",
            FileStream: Stream.Null,
            FileSizeBytes: 1024L,
            FileHash: "abc123");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<MigrationDomainException>();
        ex.Which.ErrorCode.Should().Be("MIG-ERR-001");

        await _repository.DidNotReceive()
            .AddAsync(Arg.Any<MigrationJob>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "MIG-ERR-002: colunas ausentes rejeitadas sem escrita, com lista de esperadas")]
    public async Task Handle_WhenRequiredColumnsMissing_ThrowsMigErrWithExpectedColumns()
    {
        // Arrange
        var expectedColumns = SpreadsheetStructureValidator.RequiredPipelineColumns;
        var structureWithMissingColumns = new SpreadsheetStructure(
            PipelineColumns: ["BU", "Conta"],  // faltam colunas obrigatórias
            AcoesColumns: [],
            DetectedRowCount: 10);

        _parser.ParseStructureAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(structureWithMissingColumns);

        var command = new UploadSpreadsheetCommand(
            FileName: "pipeline.xlsx",
            FileStream: new MemoryStream([1, 2, 3]),
            FileSizeBytes: 3L,
            FileHash: "def456");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<MigrationDomainException>();
        ex.Which.ErrorCode.Should().Be("MIG-ERR-002");
        ex.Which.Message.Should().Contain("BU"); // deve citar ao menos uma coluna esperada

        await _repository.DidNotReceive()
            .AddAsync(Arg.Any<MigrationJob>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "MIG-ERR-003: arquivo acima do limite rejeitado sem escrita")]
    public async Task Handle_WhenFileTooLarge_ThrowsMigErrWithoutWrite()
    {
        // Arrange
        var command = new UploadSpreadsheetCommand(
            FileName: "pipeline.xlsx",
            FileStream: new MemoryStream([1]),
            FileSizeBytes: UploadSpreadsheetHandler.MaxFileSizeBytes + 1,
            FileHash: "ghi789");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<MigrationDomainException>();
        ex.Which.ErrorCode.Should().Be("MIG-ERR-003");

        await _repository.DidNotReceive()
            .AddAsync(Arg.Any<MigrationJob>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // ST-01 (d) — Upload válido cria job em Created
    // =========================================================================

    [Fact(DisplayName = "Upload válido cria exatamente 1 MigrationJob em estado Created")]
    public async Task Handle_WhenValidXlsx_CreatesMigrationJobInCreatedState()
    {
        // Arrange
        var structure = new SpreadsheetStructure(
            PipelineColumns: SpreadsheetStructureValidator.RequiredPipelineColumns.ToList(),
            AcoesColumns: ["Ação", "Data"],
            DetectedRowCount: 108);

        _parser.ParseStructureAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(structure);

        MigrationJob? capturedJob = null;
        await _repository.AddAsync(
            Arg.Do<MigrationJob>(j => capturedJob = j),
            Arg.Any<CancellationToken>());

        var command = new UploadSpreadsheetCommand(
            FileName: "pipeline.xlsx",
            FileStream: new MemoryStream([1, 2, 3]),
            FileSizeBytes: 3L,
            FileHash: "sha256-abc");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.JobId.Should().NotBe(Guid.Empty);
        result.Status.Should().Be("created");

        capturedJob.Should().NotBeNull();
        capturedJob!.Status.Should().Be(MigrationJobStatus.Created);
        capturedJob.TenantId.Should().Be(_tenantId);
        capturedJob.SourceFileName.Should().Be("pipeline.xlsx");
        capturedJob.SourceFileSizeBytes.Should().Be(3L);
        capturedJob.SourceFileHash.Should().Be("sha256-abc");
        capturedJob.DetectedRowCount.Should().Be(108);
        capturedJob.CreatedBy.Should().Be(_userId);

        await _repository.Received(1)
            .AddAsync(Arg.Any<MigrationJob>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Upload válido não persiste conteúdo de domínio (apenas metadados)")]
    public async Task Handle_WhenValidXlsx_PersistsOnlyMetadata()
    {
        // Arrange
        var structure = new SpreadsheetStructure(
            PipelineColumns: SpreadsheetStructureValidator.RequiredPipelineColumns.ToList(),
            AcoesColumns: [],
            DetectedRowCount: 50);

        _parser.ParseStructureAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(structure);

        MigrationJob? capturedJob = null;
        await _repository.AddAsync(
            Arg.Do<MigrationJob>(j => capturedJob = j),
            Arg.Any<CancellationToken>());

        var command = new UploadSpreadsheetCommand(
            FileName: "pipeline.xlsx",
            FileStream: new MemoryStream([0xAB]),
            FileSizeBytes: 1L,
            FileHash: "hash-xyz");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert: conteúdo de domínio (oportunidades, contas) não foi persistido
        // — o job recém-criado deve ter LogEntries vazio
        capturedJob!.LogEntries.Should().BeEmpty();
        capturedJob.TriageReportJson.Should().BeNull();
        capturedJob.ImportReportJson.Should().BeNull();
    }
}
