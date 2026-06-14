using System.Text.Json;
using DataMigration.Application.Commands.Triage;
using DataMigration.Application.DTOs;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using DataMigration.Domain.Aggregates;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DataMigration.Application.Tests.Commands;

/// <summary>
/// Testes dos handlers de triagem assistida.
///
/// Rastreia: TASK-10, design §5.1, Req 4, Req 5, Req 7.2, DD-007.
/// </summary>
public sealed class TriageCommandsTests
{
    private readonly IMigrationJobRepository _repository;
    private readonly IClock _clock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 6, 14, 14, 0, 0, TimeSpan.Zero);

    public TriageCommandsTests()
    {
        _repository = Substitute.For<IMigrationJobRepository>();
        _clock = Substitute.For<IClock>();
        _clock.UtcNow.Returns(_now);
    }

    private MigrationJob BuildJobInTriageInProgress()
    {
        var job = MigrationJob.Create(
            tenantId: _tenantId,
            sourceFileName: "pipeline.xlsx",
            sourceFileSizeBytes: 1024L,
            sourceFileHash: "sha256-test",
            detectedRowCount: 5,
            createdBy: _userId,
            createdAt: _now.AddHours(-1));

        job.TransitionTo(MigrationJobStatus.DryRunCompleted, _now.AddMinutes(-30));
        job.TransitionTo(MigrationJobStatus.TriageInProgress, _now.AddMinutes(-20));
        job.SetTriageReport("{\"totalOpportunities\": 5}", _now.AddMinutes(-30));

        return job;
    }

    // =========================================================================
    // AssignOwnerCommand
    // =========================================================================

    [Fact(DisplayName = "AssignOwnerCommand atribui owner a uma oportunidade triada")]
    public async Task AssignOwner_ShouldUpdateResolution()
    {
        // Arrange
        var job = BuildJobInTriageInProgress();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var ownerId = Guid.NewGuid();
        var command = new AssignOwnerCommand(job.Id, RowIndex: 0, OwnerId: ownerId);
        var handler = new AssignOwnerHandler(_repository, _clock);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RowIndex.Should().Be(0);
        result.OwnerId.Should().Be(ownerId);

        await _repository.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // BulkAssignOwnerCommand
    // =========================================================================

    [Fact(DisplayName = "BulkAssignOwnerCommand atribui owner a todas as linhas de uma BU")]
    public async Task BulkAssignOwner_ShouldAssignAllRowsInBu()
    {
        // Arrange
        var job = BuildJobInTriageInProgress();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var ownerId = Guid.NewGuid();
        var rowIndexes = new[] { 0, 2, 4 };
        var command = new BulkAssignOwnerCommand(job.Id, BuName: "Sertão", OwnerId: ownerId, RowIndexes: rowIndexes);
        var handler = new BulkAssignOwnerHandler(_repository, _clock);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.AssignedCount.Should().Be(3);
        await _repository.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // MarkReadyToImportCommand — bloqueio quando owner faltante
    // =========================================================================

    [Fact(DisplayName = "MarkReadyToImportCommand rejeita com MIG-ERR-006 quando owner faltante")]
    public async Task MarkReadyToImport_WhenOwnerMissing_ThrowsMigErr006()
    {
        // Arrange
        var job = BuildJobInTriageInProgress();
        // Sem owners atribuídos — ownerless count = detectedRowCount (5)
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var command = new MarkReadyToImportCommand(job.Id);
        var handler = new MarkReadyToImportHandler(_repository, _clock);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<MigrationDomainException>();
        ex.Which.ErrorCode.Should().Be("MIG-ERR-006");

        // Job permanece em TriageInProgress
        job.Status.Should().Be(MigrationJobStatus.TriageInProgress);
    }

    [Fact(DisplayName = "MarkReadyToImportCommand aceita quando todos os owners atribuídos")]
    public async Task MarkReadyToImport_WhenAllOwnersAssigned_TransitionsToReadyToImport()
    {
        // Arrange
        var job = BuildJobInTriageInProgress();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        // Atribui owners a todas as 5 linhas
        var resolution = new TriageResolutionDto
        {
            OwnerAssignments = Enumerable.Range(0, 5)
                .Select(i => new OwnerAssignment(i, Guid.NewGuid()))
                .ToList()
                .AsReadOnly(),
        };
        job.SetTriageResolution(JsonSerializer.Serialize(resolution), _now);

        var command = new MarkReadyToImportCommand(job.Id);
        var handler = new MarkReadyToImportHandler(_repository, _clock);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be("ready_to_import");
        job.Status.Should().Be(MigrationJobStatus.ReadyToImport);
        await _repository.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // SaveTriageProgressCommand — snapshot sem PII
    // =========================================================================

    [Fact(DisplayName = "SaveTriageProgressCommand persiste snapshot sem PII")]
    public async Task SaveTriageProgress_PersistsSnapshotWithoutPii()
    {
        // Arrange
        var job = BuildJobInTriageInProgress();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var resolution = new TriageResolutionDto
        {
            OwnerAssignments =
            [
                new OwnerAssignment(0, Guid.NewGuid()),
                new OwnerAssignment(1, Guid.NewGuid()),
            ],
        };

        var command = new SaveTriageProgressCommand(job.Id, resolution);
        var handler = new SaveTriageProgressHandler(_repository, _clock);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: snapshot persistido no job
        job.TriageResolutionJson.Should().NotBeNullOrEmpty();

        // Verifica ausência de PII (nomes, e-mails) — snapshot contém apenas GUIDs e índices
        job.TriageResolutionJson.Should().NotContain("@");
        job.TriageResolutionJson.Should().NotContain("milton");
        job.TriageResolutionJson.Should().NotContain("email");

        await _repository.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // ResolveStagePendingCommand — não bloqueante
    // =========================================================================

    [Fact(DisplayName = "ResolveStagePendingCommand resolve estágio faltante sem bloquear import")]
    public async Task ResolveStage_ShouldUpdateResolutionAsNonBlocking()
    {
        // Arrange
        var job = BuildJobInTriageInProgress();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var stageId = Guid.NewGuid();
        var command = new ResolveStagePendingCommand(job.Id, RowIndex: 2, StageId: stageId);
        var handler = new ResolveStagePendingHandler(_repository, _clock);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.RowIndex.Should().Be(2);
        result.StageId.Should().Be(stageId);
        await _repository.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // ResolvePartnerPctCommand — não bloqueante
    // =========================================================================

    [Fact(DisplayName = "ResolvePartnerPctCommand resolve parceiro sem bloquear import")]
    public async Task ResolvePartnerPct_ShouldUpdateResolutionAsNonBlocking()
    {
        // Arrange
        var job = BuildJobInTriageInProgress();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var command = new ResolvePartnerPctCommand(job.Id, RowIndex: 1, PartnerId: null);
        var handler = new ResolvePartnerPctHandler(_repository, _clock);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.RowIndex.Should().Be(1);
        result.PartnerId.Should().BeNull();
        await _repository.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // ResolveDedupeCommand
    // =========================================================================

    [Fact(DisplayName = "ResolveDedupeCommand registra decisão de merge/manter")]
    public async Task ResolveDedupe_ShouldPersistDecision()
    {
        // Arrange
        var job = BuildJobInTriageInProgress();
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var command = new ResolveDedupeCommand(job.Id, RowIndexA: 0, RowIndexB: 3, MergeIntoRowA: true);
        var handler = new ResolveDedupeHandler(_repository, _clock);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.RowIndexA.Should().Be(0);
        result.RowIndexB.Should().Be(3);
        result.MergeIntoRowA.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
    }
}
