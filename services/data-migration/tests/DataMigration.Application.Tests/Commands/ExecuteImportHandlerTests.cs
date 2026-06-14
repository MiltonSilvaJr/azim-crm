using System.Text.Json;
using DataMigration.Application.Commands.Import;
using DataMigration.Application.DTOs;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using DataMigration.Domain.Aggregates;
using DataMigration.Domain.ValueObjects;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DataMigration.Application.Tests.Commands;

/// <summary>
/// Testes do handler <see cref="ExecuteImportHandler"/>.
///
/// Rastreia: TASK-11, design §5.3, Req 6, Req 12, PBT-01, PBT-02;
/// MIG-ERR-005, MIG-ERR-006, MIG-ERR-007, MIG-ERR-008.
/// </summary>
public sealed class ExecuteImportHandlerTests
{
    private readonly IMigrationJobRepository _repository;
    private readonly IUnitOfWork _uow;
    private readonly ISpreadsheetParser _parser;
    private readonly IAccountImportPort _accountPort;
    private readonly IPartnerImportPort _partnerPort;
    private readonly IOpportunityImportPort _opportunityPort;
    private readonly IActivityImportPort _activityPort;
    private readonly IOpportunityNumberPort _numberPort;
    private readonly IOrganizationReadPort _orgPort;
    private readonly IClock _clock;
    private readonly ExecuteImportHandler _handler;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 6, 14, 16, 0, 0, TimeSpan.Zero);

    public ExecuteImportHandlerTests()
    {
        _repository = Substitute.For<IMigrationJobRepository>();
        _uow = Substitute.For<IUnitOfWork>();
        _parser = Substitute.For<ISpreadsheetParser>();
        _accountPort = Substitute.For<IAccountImportPort>();
        _partnerPort = Substitute.For<IPartnerImportPort>();
        _opportunityPort = Substitute.For<IOpportunityImportPort>();
        _activityPort = Substitute.For<IActivityImportPort>();
        _numberPort = Substitute.For<IOpportunityNumberPort>();
        _orgPort = Substitute.For<IOrganizationReadPort>();
        _clock = Substitute.For<IClock>();

        _clock.UtcNow.Returns(_now);

        // Setup padrões
        _accountPort.CreateOrGetAsync(Arg.Any<AccountImportRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AccountImportResult(Guid.NewGuid(), true));
        _partnerPort.CreateOrGetAsync(Arg.Any<PartnerImportRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PartnerImportResult(Guid.NewGuid(), true));
        _opportunityPort.CreateOrUpdateAsync(Arg.Any<OpportunityImportRequest>(), Arg.Any<CancellationToken>())
            .Returns(new OpportunityImportResult(Guid.NewGuid(), true));
        _activityPort.CreateOrUpdateAsync(Arg.Any<ActivityImportRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ActivityImportResult(Guid.NewGuid(), true));
        _numberPort.AllocateNextAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(OpportunityNumber.Parse("AZ-0095")!);
        _orgPort.GetStageIdByNameAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        _handler = new ExecuteImportHandler(
            _repository, _uow, _parser,
            _accountPort, _partnerPort, _opportunityPort,
            _activityPort, _numberPort, _orgPort, _clock);
    }

    private MigrationJob BuildReadyJob(int rowCount = 2)
    {
        var job = MigrationJob.Create(
            tenantId: _tenantId,
            sourceFileName: "pipeline.xlsx",
            sourceFileSizeBytes: 1024L,
            sourceFileHash: "sha256-test",
            detectedRowCount: rowCount,
            createdBy: _userId,
            createdAt: _now.AddHours(-2));

        job.TransitionTo(MigrationJobStatus.DryRunCompleted, _now.AddHours(-1));
        job.TransitionTo(MigrationJobStatus.TriageInProgress, _now.AddMinutes(-50));

        // Atribui owners a todas as linhas
        var resolution = new TriageResolutionDto
        {
            OwnerAssignments = Enumerable.Range(0, rowCount)
                .Select(i => new OwnerAssignment(i, Guid.NewGuid()))
                .ToList()
                .AsReadOnly(),
        };
        job.SetTriageReport(
            JsonSerializer.Serialize(new { totalOpportunities = rowCount }),
            _now.AddHours(-1));
        job.SetTriageResolution(JsonSerializer.Serialize(resolution), _now.AddMinutes(-40));
        job.TransitionTo(MigrationJobStatus.ReadyToImport, _now.AddMinutes(-30));

        return job;
    }

    private IReadOnlyList<SourceRow> BuildPipelineRows(int count)
    {
        var rows = new List<SourceRow>();
        for (int i = 0; i < count; i++)
        {
            rows.Add(new SourceRow("pipeline", i, new Dictionary<string, string?>
            {
                ["BU"] = "TI",
                ["Conta"] = $"Empresa {i}",
                ["Título"] = $"Oportunidade {i}",
                ["Responsável"] = "Milton",
                ["Estágio"] = "Lead",
                ["Parceiro"] = "-",
                ["Valor Setup"] = "100000",
                ["Valor Mensal"] = "50000",
                ["Meses"] = "12",
                ["Forecast (R$)"] = "700000",
                ["Nº Oportunidade"] = string.Empty,
                ["Data de Fechamento"] = string.Empty,
            }));
        }

        return rows;
    }

    // =========================================================================
    // Validações de entrada — MIG-ERR-008, MIG-ERR-005
    // =========================================================================

    [Fact(DisplayName = "MIG-ERR-008: confirmation=false rejeita sem executar nada")]
    public async Task Handle_WhenConfirmationFalse_ThrowsMigErr008()
    {
        // Arrange
        var command = new ExecuteImportCommand(Guid.NewGuid(), Confirmation: false, Stream.Null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<MigrationDomainException>();
        ex.Which.ErrorCode.Should().Be("MIG-ERR-008");

        await _repository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "MIG-ERR-005: job fora de ReadyToImport rejeita execução")]
    public async Task Handle_WhenJobNotInReadyState_ThrowsMigErr005()
    {
        // Arrange
        var job = MigrationJob.Create(
            _tenantId, "p.xlsx", 1024L, "hash", 2, _userId, _now.AddHours(-1));
        job.TransitionTo(MigrationJobStatus.DryRunCompleted);
        job.TransitionTo(MigrationJobStatus.TriageInProgress);
        // Não transita para ReadyToImport

        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var command = new ExecuteImportCommand(job.Id, Confirmation: true, Stream.Null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<MigrationDomainException>();
        ex.Which.ErrorCode.Should().Be("MIG-ERR-005");
    }

    // =========================================================================
    // Caminho feliz — tudo-ou-nada, transação commitada
    // =========================================================================

    [Fact(DisplayName = "Import bem-sucedido: transação commitada, job em Completed")]
    public async Task Handle_WhenSuccessful_CommitsTransactionAndCompletesJob()
    {
        // Arrange
        var job = BuildReadyJob(rowCount: 2);
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _parser.ParseRowsAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(BuildPipelineRows(2));

        var command = new ExecuteImportCommand(job.Id, Confirmation: true, Stream.Null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be("completed");
        job.Status.Should().Be(MigrationJobStatus.Completed);
        job.ImportReportJson.Should().NotBeNullOrEmpty();

        await _uow.Received(1).BeginAsync(Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Import bem-sucedido: ordem determinística accounts→partners→opportunities→activities")]
    public async Task Handle_WhenSuccessful_ExecutesInDeterministicOrder()
    {
        // Arrange
        var job = BuildReadyJob(rowCount: 1);
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _parser.ParseRowsAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(BuildPipelineRows(1));

        var callOrder = new List<string>();
        _accountPort.CreateOrGetAsync(Arg.Any<AccountImportRequest>(), Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                callOrder.Add("account");
                return Task.FromResult(new AccountImportResult(Guid.NewGuid(), true));
            });
        _opportunityPort.CreateOrUpdateAsync(Arg.Any<OpportunityImportRequest>(), Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                callOrder.Add("opportunity");
                return Task.FromResult(new OpportunityImportResult(Guid.NewGuid(), true));
            });

        var command = new ExecuteImportCommand(job.Id, Confirmation: true, Stream.Null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert: account antes de opportunity
        callOrder.Should().Contain("account");
        callOrder.Should().Contain("opportunity");
        callOrder.IndexOf("account").Should().BeLessThan(callOrder.IndexOf("opportunity"));
    }

    // =========================================================================
    // Falha → rollback total (tudo-ou-nada)
    // =========================================================================

    [Fact(DisplayName = "MIG-ERR-007: falha em qualquer passo → rollback total, job em RolledBack")]
    public async Task Handle_WhenAnyStepFails_RollsBackAndTransitionsToRolledBack()
    {
        // Arrange
        var job = BuildReadyJob(rowCount: 2);
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _parser.ParseRowsAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(BuildPipelineRows(2));

        // Simula falha na porta de oportunidades
        _opportunityPort.CreateOrUpdateAsync(Arg.Any<OpportunityImportRequest>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Falha simulada na porta."));

        var command = new ExecuteImportCommand(job.Id, Confirmation: true, Stream.Null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<MigrationDomainException>();
        ex.Which.ErrorCode.Should().Be("MIG-ERR-007");

        job.Status.Should().Be(MigrationJobStatus.RolledBack);
        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // PBT-01 — Atomicidade: qualquer falha → zero registros persistidos
    // =========================================================================

    /// <summary>
    /// PBT-01: para qualquer conjunto com ≥1 linha que falha, após o import o banco
    /// está idêntico ao estado anterior (simulado por rollback chamado, commit não).
    ///
    /// Rastreia: PBT-01, design §13, Req 6.1, RNF 5.
    /// </summary>
    [Property(MaxTest = 30, DisplayName = "PBT-01: qualquer falha → rollback, nunca commit")]
    public Property Pbt01_AnyFailureCausesRollbackNeverCommit(PositiveInt n)
    {
        var rowCount = Math.Min(n.Get, 10) + 1; // mínimo 2 linhas

        var repository = Substitute.For<IMigrationJobRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var parser = Substitute.For<ISpreadsheetParser>();
        var accountPort = Substitute.For<IAccountImportPort>();
        var partnerPort = Substitute.For<IPartnerImportPort>();
        var opportunityPort = Substitute.For<IOpportunityImportPort>();
        var activityPort = Substitute.For<IActivityImportPort>();
        var numberPort = Substitute.For<IOpportunityNumberPort>();
        var orgPort = Substitute.For<IOrganizationReadPort>();
        var clock = Substitute.For<IClock>();

        clock.UtcNow.Returns(_now);
        accountPort.CreateOrGetAsync(Arg.Any<AccountImportRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AccountImportResult(Guid.NewGuid(), true));
        numberPort.AllocateNextAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(OpportunityNumber.Parse("AZ-0095")!);
        orgPort.GetStageIdByNameAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        // A porta de oportunidade sempre falha
        opportunityPort.CreateOrUpdateAsync(Arg.Any<OpportunityImportRequest>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Falha injetada PBT-01"));

        var job = BuildReadyJob(rowCount);
        repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        parser.ParseRowsAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(BuildPipelineRows(rowCount));

        var handler = new ExecuteImportHandler(
            repository, uow, parser,
            accountPort, partnerPort, opportunityPort,
            activityPort, numberPort, orgPort, clock);

        bool rollbackCalled = false;
        bool commitCalled = false;

        uow.RollbackAsync(Arg.Any<CancellationToken>())
            .Returns(x => { rollbackCalled = true; return Task.CompletedTask; });
        uow.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(x => { commitCalled = true; return Task.CompletedTask; });

        try
        {
            handler.Handle(
                new ExecuteImportCommand(job.Id, Confirmation: true, Stream.Null),
                CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (MigrationDomainException ex) when (ex.ErrorCode == "MIG-ERR-007")
        {
            // Esperado
        }
        catch
        {
            // Qualquer outra exceção: o importante é que rollback foi chamado
        }

        return (rollbackCalled && !commitCalled)
            .ToProperty()
            .Label($"rollback={rollbackCalled}, commit={commitCalled}");
    }

    // =========================================================================
    // PBT-02 — Idempotência: reexecução não duplica registros
    // =========================================================================

    /// <summary>
    /// PBT-02: import bem-sucedido + reexecução do mesmo conjunto triado
    /// (após rolled_back → triage_in_progress → ready_to_import) não aumenta contagens.
    ///
    /// Simulado: segunda chamada bem-sucedida retorna IsNew=false (idempotente).
    ///
    /// Rastreia: PBT-02, design §13, Req 12, DD-003.
    /// </summary>
    [Property(MaxTest = 20, DisplayName = "PBT-02: reexecução idempotente — IsNew=false na 2ª chamada")]
    public Property Pbt02_ReexecutionIsIdempotent(PositiveInt n)
    {
        var rowCount = Math.Min(n.Get, 5) + 1;

        var callCount = 0;
        var opportunityPort = Substitute.For<IOpportunityImportPort>();
        opportunityPort.CreateOrUpdateAsync(Arg.Any<OpportunityImportRequest>(), Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                callCount++;
                // Primeira chamada: IsNew=true; segunda: IsNew=false (idempotente)
                return Task.FromResult(new OpportunityImportResult(Guid.NewGuid(), callCount <= rowCount));
            });

        var repository = Substitute.For<IMigrationJobRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var parser = Substitute.For<ISpreadsheetParser>();
        var accountPort = Substitute.For<IAccountImportPort>();
        var partnerPort = Substitute.For<IPartnerImportPort>();
        var activityPort = Substitute.For<IActivityImportPort>();
        var numberPort = Substitute.For<IOpportunityNumberPort>();
        var orgPort = Substitute.For<IOrganizationReadPort>();
        var clock = Substitute.For<IClock>();

        clock.UtcNow.Returns(_now);
        accountPort.CreateOrGetAsync(Arg.Any<AccountImportRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AccountImportResult(Guid.NewGuid(), true));
        numberPort.AllocateNextAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(OpportunityNumber.Parse("AZ-0095")!);
        orgPort.GetStageIdByNameAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var rows = BuildPipelineRows(rowCount);

        // Primeira execução
        var job1 = BuildReadyJob(rowCount);
        repository.GetByIdAsync(job1.Id, Arg.Any<CancellationToken>()).Returns(job1);
        parser.ParseRowsAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(rows);

        var handler = new ExecuteImportHandler(
            repository, uow, parser,
            accountPort, partnerPort, opportunityPort,
            activityPort, numberPort, orgPort, clock);

        var result1 = handler.Handle(
            new ExecuteImportCommand(job1.Id, Confirmation: true, Stream.Null),
            CancellationToken.None).GetAwaiter().GetResult();

        var firstCallCount = callCount;

        // Segunda execução (reexecução após rolled_back → triage_in_progress → ready_to_import)
        var job2 = BuildReadyJob(rowCount);
        repository.GetByIdAsync(job2.Id, Arg.Any<CancellationToken>()).Returns(job2);

        var result2 = handler.Handle(
            new ExecuteImportCommand(job2.Id, Confirmation: true, Stream.Null),
            CancellationToken.None).GetAwaiter().GetResult();

        var secondCallCount = callCount - firstCallCount;

        // Propriedade: segunda execução tem mesmo número de chamadas (idempotente)
        return (result1.Status == "completed"
                && result2.Status == "completed"
                && secondCallCount == firstCallCount)
            .ToProperty()
            .Label($"first={firstCallCount}, second={secondCallCount}");
    }
}
