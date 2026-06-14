using DataMigration.Domain.Aggregates;
using DataMigration.Domain.Exceptions;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataMigration.Domain.Tests.Aggregates;

/// <summary>
/// Testes unitários do agregado MigrationJob e sua máquina de estados.
///
/// Cobre: TASK-03 (ST-01), Req 4, Req 6, Req 12, PBT-07.
/// Transições válidas e inválidas; estados terminais; CanTransitionToImporting.
/// </summary>
public sealed class MigrationJobTests
{
    // =========================================================================
    // Fábrica auxiliar
    // =========================================================================

    private static MigrationJob CreateJob()
    {
        return MigrationJob.Create(
            tenantId: Guid.NewGuid(),
            sourceFileName: "Pipeline.xlsx",
            sourceFileSizeBytes: 204_800L,
            sourceFileHash: "abc123",
            detectedRowCount: 108,
            createdBy: Guid.NewGuid());
    }

    // =========================================================================
    // Criação
    // =========================================================================

    [Fact(DisplayName = "MigrationJob.Create deve iniciar em estado created")]
    public void Create_ShouldStart_InCreatedState()
    {
        var job = CreateJob();
        job.Status.Should().Be(MigrationJobStatus.Created);
    }

    [Fact(DisplayName = "MigrationJob.Create deve persistir metadados imutáveis do arquivo")]
    public void Create_ShouldPersist_FileMetadata()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var job = MigrationJob.Create(
            tenantId: tenantId,
            sourceFileName: "Pipeline.xlsx",
            sourceFileSizeBytes: 204_800L,
            sourceFileHash: "abc123",
            detectedRowCount: 108,
            createdBy: createdBy);

        job.TenantId.Should().Be(tenantId);
        job.SourceFileName.Should().Be("Pipeline.xlsx");
        job.SourceFileSizeBytes.Should().Be(204_800L);
        job.SourceFileHash.Should().Be("abc123");
        job.DetectedRowCount.Should().Be(108);
        job.CreatedBy.Should().Be(createdBy);
    }

    // =========================================================================
    // Transições válidas (design §4.5)
    // =========================================================================

    [Fact(DisplayName = "created -> dry_run_completed deve ser válida")]
    public void Transition_CreatedToDryRunCompleted_ShouldSucceed()
    {
        var job = CreateJob();
        job.TransitionTo(MigrationJobStatus.DryRunCompleted);
        job.Status.Should().Be(MigrationJobStatus.DryRunCompleted);
    }

    [Fact(DisplayName = "created -> failed deve ser válida")]
    public void Transition_CreatedToFailed_ShouldSucceed()
    {
        var job = CreateJob();
        job.TransitionTo(MigrationJobStatus.Failed);
        job.Status.Should().Be(MigrationJobStatus.Failed);
    }

    [Fact(DisplayName = "dry_run_completed -> triage_in_progress deve ser válida")]
    public void Transition_DryRunCompletedToTriageInProgress_ShouldSucceed()
    {
        var job = CreateJob();
        job.TransitionTo(MigrationJobStatus.DryRunCompleted);
        job.TransitionTo(MigrationJobStatus.TriageInProgress);
        job.Status.Should().Be(MigrationJobStatus.TriageInProgress);
    }

    [Fact(DisplayName = "dry_run_completed -> failed deve ser válida")]
    public void Transition_DryRunCompletedToFailed_ShouldSucceed()
    {
        var job = CreateJob();
        job.TransitionTo(MigrationJobStatus.DryRunCompleted);
        job.TransitionTo(MigrationJobStatus.Failed);
        job.Status.Should().Be(MigrationJobStatus.Failed);
    }

    [Fact(DisplayName = "triage_in_progress -> triage_in_progress (salvar/retomar) deve ser válida")]
    public void Transition_TriageInProgressToTriageInProgress_ShouldSucceed()
    {
        var job = CreateJob();
        job.TransitionTo(MigrationJobStatus.DryRunCompleted);
        job.TransitionTo(MigrationJobStatus.TriageInProgress);
        job.TransitionTo(MigrationJobStatus.TriageInProgress);
        job.Status.Should().Be(MigrationJobStatus.TriageInProgress);
    }

    [Fact(DisplayName = "triage_in_progress -> ready_to_import deve ser válida")]
    public void Transition_TriageInProgressToReadyToImport_ShouldSucceed()
    {
        var job = CreateJob();
        job.TransitionTo(MigrationJobStatus.DryRunCompleted);
        job.TransitionTo(MigrationJobStatus.TriageInProgress);
        job.TransitionTo(MigrationJobStatus.ReadyToImport);
        job.Status.Should().Be(MigrationJobStatus.ReadyToImport);
    }

    [Fact(DisplayName = "ready_to_import -> triage_in_progress (reabrir) deve ser válida")]
    public void Transition_ReadyToImportToTriageInProgress_ShouldSucceed()
    {
        var job = CreateJob();
        job.TransitionTo(MigrationJobStatus.DryRunCompleted);
        job.TransitionTo(MigrationJobStatus.TriageInProgress);
        job.TransitionTo(MigrationJobStatus.ReadyToImport);
        job.TransitionTo(MigrationJobStatus.TriageInProgress);
        job.Status.Should().Be(MigrationJobStatus.TriageInProgress);
    }

    [Fact(DisplayName = "ready_to_import -> importing deve ser válida")]
    public void Transition_ReadyToImportToImporting_ShouldSucceed()
    {
        var job = CreateJob();
        job.TransitionTo(MigrationJobStatus.DryRunCompleted);
        job.TransitionTo(MigrationJobStatus.TriageInProgress);
        job.TransitionTo(MigrationJobStatus.ReadyToImport);
        job.TransitionTo(MigrationJobStatus.Importing);
        job.Status.Should().Be(MigrationJobStatus.Importing);
    }

    [Fact(DisplayName = "importing -> completed deve ser válida")]
    public void Transition_ImportingToCompleted_ShouldSucceed()
    {
        var job = CreateJob();
        job.TransitionTo(MigrationJobStatus.DryRunCompleted);
        job.TransitionTo(MigrationJobStatus.TriageInProgress);
        job.TransitionTo(MigrationJobStatus.ReadyToImport);
        job.TransitionTo(MigrationJobStatus.Importing);
        job.TransitionTo(MigrationJobStatus.Completed);
        job.Status.Should().Be(MigrationJobStatus.Completed);
    }

    [Fact(DisplayName = "importing -> rolled_back deve ser válida")]
    public void Transition_ImportingToRolledBack_ShouldSucceed()
    {
        var job = CreateJob();
        job.TransitionTo(MigrationJobStatus.DryRunCompleted);
        job.TransitionTo(MigrationJobStatus.TriageInProgress);
        job.TransitionTo(MigrationJobStatus.ReadyToImport);
        job.TransitionTo(MigrationJobStatus.Importing);
        job.TransitionTo(MigrationJobStatus.RolledBack);
        job.Status.Should().Be(MigrationJobStatus.RolledBack);
    }

    [Fact(DisplayName = "rolled_back -> triage_in_progress (nova tentativa) deve ser válida")]
    public void Transition_RolledBackToTriageInProgress_ShouldSucceed()
    {
        var job = CreateJob();
        job.TransitionTo(MigrationJobStatus.DryRunCompleted);
        job.TransitionTo(MigrationJobStatus.TriageInProgress);
        job.TransitionTo(MigrationJobStatus.ReadyToImport);
        job.TransitionTo(MigrationJobStatus.Importing);
        job.TransitionTo(MigrationJobStatus.RolledBack);
        job.TransitionTo(MigrationJobStatus.TriageInProgress);
        job.Status.Should().Be(MigrationJobStatus.TriageInProgress);
    }

    // =========================================================================
    // Transições inválidas (design §4.5, MIG-ERR-005)
    // =========================================================================

    [Theory(DisplayName = "Transições inválidas devem lançar InvalidMigrationStateTransitionException (MIG-ERR-005)")]
    [InlineData(MigrationJobStatus.Created, MigrationJobStatus.TriageInProgress)]
    [InlineData(MigrationJobStatus.Created, MigrationJobStatus.ReadyToImport)]
    [InlineData(MigrationJobStatus.Created, MigrationJobStatus.Importing)]
    [InlineData(MigrationJobStatus.Created, MigrationJobStatus.Completed)]
    [InlineData(MigrationJobStatus.Created, MigrationJobStatus.RolledBack)]
    [InlineData(MigrationJobStatus.DryRunCompleted, MigrationJobStatus.Created)]
    [InlineData(MigrationJobStatus.DryRunCompleted, MigrationJobStatus.ReadyToImport)]
    [InlineData(MigrationJobStatus.DryRunCompleted, MigrationJobStatus.Importing)]
    [InlineData(MigrationJobStatus.DryRunCompleted, MigrationJobStatus.Completed)]
    [InlineData(MigrationJobStatus.DryRunCompleted, MigrationJobStatus.RolledBack)]
    [InlineData(MigrationJobStatus.TriageInProgress, MigrationJobStatus.Created)]
    [InlineData(MigrationJobStatus.TriageInProgress, MigrationJobStatus.DryRunCompleted)]
    [InlineData(MigrationJobStatus.TriageInProgress, MigrationJobStatus.Importing)]
    [InlineData(MigrationJobStatus.TriageInProgress, MigrationJobStatus.Completed)]
    [InlineData(MigrationJobStatus.TriageInProgress, MigrationJobStatus.RolledBack)]
    [InlineData(MigrationJobStatus.TriageInProgress, MigrationJobStatus.Failed)]
    [InlineData(MigrationJobStatus.ReadyToImport, MigrationJobStatus.Created)]
    [InlineData(MigrationJobStatus.ReadyToImport, MigrationJobStatus.DryRunCompleted)]
    [InlineData(MigrationJobStatus.ReadyToImport, MigrationJobStatus.Completed)]
    [InlineData(MigrationJobStatus.ReadyToImport, MigrationJobStatus.RolledBack)]
    [InlineData(MigrationJobStatus.ReadyToImport, MigrationJobStatus.Failed)]
    [InlineData(MigrationJobStatus.Importing, MigrationJobStatus.Created)]
    [InlineData(MigrationJobStatus.Importing, MigrationJobStatus.DryRunCompleted)]
    [InlineData(MigrationJobStatus.Importing, MigrationJobStatus.TriageInProgress)]
    [InlineData(MigrationJobStatus.Importing, MigrationJobStatus.ReadyToImport)]
    [InlineData(MigrationJobStatus.Importing, MigrationJobStatus.Failed)]
    [InlineData(MigrationJobStatus.RolledBack, MigrationJobStatus.Created)]
    [InlineData(MigrationJobStatus.RolledBack, MigrationJobStatus.DryRunCompleted)]
    [InlineData(MigrationJobStatus.RolledBack, MigrationJobStatus.ReadyToImport)]
    [InlineData(MigrationJobStatus.RolledBack, MigrationJobStatus.Importing)]
    [InlineData(MigrationJobStatus.RolledBack, MigrationJobStatus.Completed)]
    [InlineData(MigrationJobStatus.RolledBack, MigrationJobStatus.RolledBack)]
    [InlineData(MigrationJobStatus.RolledBack, MigrationJobStatus.Failed)]
    public void Transition_Invalid_ShouldThrow_InvalidMigrationStateTransitionException(
        MigrationJobStatus from, MigrationJobStatus to)
    {
        var job = ReachState(from);
        var act = () => job.TransitionTo(to);
        act.Should().Throw<InvalidMigrationStateTransitionException>()
            .WithMessage($"*{from}*")
            .And.ErrorCode.Should().Be("MIG-ERR-005");
    }

    // =========================================================================
    // Estados terminais (design §4.5)
    // =========================================================================

    [Theory(DisplayName = "Estados terminais (completed, failed) devem rejeitar qualquer transição")]
    [InlineData(MigrationJobStatus.Completed, MigrationJobStatus.Created)]
    [InlineData(MigrationJobStatus.Completed, MigrationJobStatus.DryRunCompleted)]
    [InlineData(MigrationJobStatus.Completed, MigrationJobStatus.TriageInProgress)]
    [InlineData(MigrationJobStatus.Completed, MigrationJobStatus.ReadyToImport)]
    [InlineData(MigrationJobStatus.Completed, MigrationJobStatus.Importing)]
    [InlineData(MigrationJobStatus.Completed, MigrationJobStatus.Completed)]
    [InlineData(MigrationJobStatus.Completed, MigrationJobStatus.RolledBack)]
    [InlineData(MigrationJobStatus.Completed, MigrationJobStatus.Failed)]
    [InlineData(MigrationJobStatus.Failed, MigrationJobStatus.Created)]
    [InlineData(MigrationJobStatus.Failed, MigrationJobStatus.DryRunCompleted)]
    [InlineData(MigrationJobStatus.Failed, MigrationJobStatus.TriageInProgress)]
    [InlineData(MigrationJobStatus.Failed, MigrationJobStatus.ReadyToImport)]
    [InlineData(MigrationJobStatus.Failed, MigrationJobStatus.Importing)]
    [InlineData(MigrationJobStatus.Failed, MigrationJobStatus.Completed)]
    [InlineData(MigrationJobStatus.Failed, MigrationJobStatus.RolledBack)]
    [InlineData(MigrationJobStatus.Failed, MigrationJobStatus.Failed)]
    public void TerminalStates_ShouldReject_AllTransitions(
        MigrationJobStatus terminal, MigrationJobStatus to)
    {
        var job = ReachState(terminal);
        var act = () => job.TransitionTo(to);
        act.Should().Throw<InvalidMigrationStateTransitionException>();
    }

    // =========================================================================
    // CanTransitionToImporting (PBT-07, design §4.5, §4.1)
    // =========================================================================

    [Fact(DisplayName = "CanTransitionToImporting deve retornar false quando há oportunidades sem owner")]
    public void CanTransitionToImporting_WithOwnerlessCandidates_ShouldReturnFalse()
    {
        var job = CreateJob();
        job.CanTransitionToImporting(ownerlessCandidateCount: 3).Should().BeFalse();
    }

    [Fact(DisplayName = "CanTransitionToImporting deve retornar true quando não há oportunidades sem owner")]
    public void CanTransitionToImporting_WithNoOwnerlessCandidates_ShouldReturnTrue()
    {
        var job = CreateJob();
        job.CanTransitionToImporting(ownerlessCandidateCount: 0).Should().BeTrue();
    }

    [Fact(DisplayName = "TransitionToImporting deve lançar quando há oportunidades sem owner (MIG-ERR-006)")]
    public void TransitionToImporting_WithOwnerlessCandidates_ShouldThrow()
    {
        var job = CreateJob();
        job.TransitionTo(MigrationJobStatus.DryRunCompleted);
        job.TransitionTo(MigrationJobStatus.TriageInProgress);
        job.TransitionTo(MigrationJobStatus.ReadyToImport);

        var act = () => job.TransitionToImporting(ownerlessCandidateCount: 5);
        act.Should().Throw<OwnerRequiredForImportException>()
            .And.ErrorCode.Should().Be("MIG-ERR-006");
    }

    [Fact(DisplayName = "TransitionToImporting deve ter sucesso quando não há oportunidades sem owner")]
    public void TransitionToImporting_WithNoOwnerlessCandidates_ShouldSucceed()
    {
        var job = CreateJob();
        job.TransitionTo(MigrationJobStatus.DryRunCompleted);
        job.TransitionTo(MigrationJobStatus.TriageInProgress);
        job.TransitionTo(MigrationJobStatus.ReadyToImport);

        job.TransitionToImporting(ownerlessCandidateCount: 0);
        job.Status.Should().Be(MigrationJobStatus.Importing);
    }

    // =========================================================================
    // PBT-07 — FsCheck: para qualquer nº ownerless > 0, transição é rejeitada
    // Mapeia: PBT-07, design §4.5, §13; TASK-03 ST-01
    // =========================================================================

    /// <summary>
    /// PBT-07: para qualquer estado de triagem (ready_to_import),
    /// a transição para importing é rejeitada se existir pelo menos
    /// uma oportunidade sem owner.
    ///
    /// Rastreia: PBT-07, design §4.5, §13.
    /// </summary>
    [Property(
        DisplayName = "PBT-07: TransitionToImporting rejeita quando ownerlessCandidateCount > 0",
        MaxTest = 200)]
    public Property Pbt07_TransitionToImporting_RejectedWhen_OwnerlessCountPositive(
        PositiveInt ownerlessCount)
    {
        var job = CreateJob();
        job.TransitionTo(MigrationJobStatus.DryRunCompleted);
        job.TransitionTo(MigrationJobStatus.TriageInProgress);
        job.TransitionTo(MigrationJobStatus.ReadyToImport);

        var count = ownerlessCount.Get;
        try
        {
            job.TransitionToImporting(ownerlessCandidateCount: count);
            return false.ToProperty().Label($"Esperava exceção para ownerless={count}");
        }
        catch (OwnerRequiredForImportException)
        {
            return true.ToProperty();
        }
    }

    [Property(
        DisplayName = "PBT-07: CanTransitionToImporting retorna false para qualquer count > 0",
        MaxTest = 200)]
    public Property Pbt07_CanTransitionToImporting_False_WhenOwnerlessPositive(
        PositiveInt ownerlessCount)
    {
        var job = CreateJob();
        return (!job.CanTransitionToImporting(ownerlessCount.Get)).ToProperty()
            .Label($"ownerless={ownerlessCount.Get}");
    }

    // =========================================================================
    // MigrationLogEntry
    // =========================================================================

    [Fact(DisplayName = "AddLogEntry deve registrar entrada de log com campos corretos")]
    public void AddLogEntry_ShouldRegister_EntryWithCorrectFields()
    {
        var job = CreateJob();
        job.AddLogEntry(
            sourceSheet: "pipeline",
            sourceRowIndex: 5,
            status: MigrationLogStatus.Ok,
            message: "Linha processada com sucesso",
            importKey: null);

        job.LogEntries.Should().HaveCount(1);
        var entry = job.LogEntries[0];
        entry.SourceSheet.Should().Be("pipeline");
        entry.SourceRowIndex.Should().Be(5);
        entry.Status.Should().Be(MigrationLogStatus.Ok);
        entry.Message.Should().Be("Linha processada com sucesso");
        entry.ImportKey.Should().BeNull();
    }

    [Fact(DisplayName = "AddLogEntry com importKey deve persistir a chave de idempotência")]
    public void AddLogEntry_WithImportKey_ShouldPersistKey()
    {
        var job = CreateJob();
        const string key = "key-abc-123";
        job.AddLogEntry("pipeline", 1, MigrationLogStatus.Ok, "ok", importKey: key);
        job.LogEntries[0].ImportKey.Should().Be(key);
    }

    // =========================================================================
    // Imutabilidade de metadados após created
    // =========================================================================

    [Fact(DisplayName = "Metadados do arquivo devem ser imutáveis após created (design §4.1)")]
    public void FileMetadata_ShouldBeImmutable_AfterCreated()
    {
        var job = MigrationJob.Create(
            tenantId: Guid.NewGuid(),
            sourceFileName: "Pipeline.xlsx",
            sourceFileSizeBytes: 1024,
            sourceFileHash: "abc",
            detectedRowCount: 10,
            createdBy: Guid.NewGuid());

        // Transição não altera metadados
        job.TransitionTo(MigrationJobStatus.DryRunCompleted);

        job.SourceFileName.Should().Be("Pipeline.xlsx");
        job.SourceFileSizeBytes.Should().Be(1024);
        job.SourceFileHash.Should().Be("abc");
        job.DetectedRowCount.Should().Be(10);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Coloca o job no estado desejado percorrendo o caminho mínimo válido.
    /// Usado apenas nos testes — não é API pública do agregado.
    /// </summary>
    private static MigrationJob ReachState(MigrationJobStatus target)
    {
        var job = CreateJob();

        if (target == MigrationJobStatus.Created)
        {
            return job;
        }

        if (target == MigrationJobStatus.Failed)
        {
            job.TransitionTo(MigrationJobStatus.Failed);
            return job;
        }

        job.TransitionTo(MigrationJobStatus.DryRunCompleted);

        if (target == MigrationJobStatus.DryRunCompleted)
        {
            return job;
        }

        job.TransitionTo(MigrationJobStatus.TriageInProgress);

        if (target == MigrationJobStatus.TriageInProgress)
        {
            return job;
        }

        job.TransitionTo(MigrationJobStatus.ReadyToImport);

        if (target == MigrationJobStatus.ReadyToImport)
        {
            return job;
        }

        job.TransitionTo(MigrationJobStatus.Importing);

        if (target == MigrationJobStatus.Importing)
        {
            return job;
        }

        if (target == MigrationJobStatus.Completed)
        {
            job.TransitionTo(MigrationJobStatus.Completed);
            return job;
        }

        if (target == MigrationJobStatus.RolledBack)
        {
            job.TransitionTo(MigrationJobStatus.RolledBack);
            return job;
        }

        throw new InvalidOperationException($"Estado {target} não atingível pelo helper.");
    }
}
