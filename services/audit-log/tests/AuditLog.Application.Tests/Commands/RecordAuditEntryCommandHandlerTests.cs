using AuditLog.Application.Abstractions;
using AuditLog.Application.Commands;
using AuditLog.Application.Handlers;
using AuditLog.Domain.Abstractions;
using AuditLog.Domain.Aggregates;
using AuditLog.Domain.Repositories;
using AuditLog.Domain.Services;
using AuditLog.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AuditLog.Application.Tests.Commands;

/// <summary>
/// Testes unitários do <see cref="AuditService"/> (handler de <see cref="RecordAuditEntryCommand"/>).
/// Verifica sequência de mascaramento → construção do aggregate → persistência.
/// </summary>
public sealed class RecordAuditEntryCommandHandlerTests
{
    private readonly IAuditLogRepository _repository = Substitute.For<IAuditLogRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IAuditMetrics _metrics = Substitute.For<IAuditMetrics>();

    private readonly Guid _tenantIdValue = Guid.NewGuid();
    private readonly DateTimeOffset _fixedNow = new(2026, 6, 12, 10, 0, 0, TimeSpan.Zero);

    public RecordAuditEntryCommandHandlerTests()
    {
        _clock.UtcNow.Returns(_fixedNow);
        _tenantContext.TenantId.Returns(_tenantIdValue);
    }

    private AuditService BuildSut(IPiiFieldPolicy? policy = null)
    {
        var effectivePolicy = policy ?? BuildNoPiiPolicy();
        return new AuditService(
            _repository,
            new PiiMasker(effectivePolicy),
            effectivePolicy,
            _clock,
            _tenantContext,
            _metrics,
            NullLogger<AuditService>.Instance);
    }

    // -----------------------------------------------------------------------
    // created_at derivado do IClock
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Handler deve definir created_at via IClock, não via chamador")]
    public async Task Handle_Should_Set_CreatedAt_From_Clock()
    {
        var sut = BuildSut();
        var cmd = ValidCreateCommand();

        AuditLogAggregate? captured = null;
        await _repository.AddAsync(
            Arg.Do<AuditLogAggregate>(a => captured = a),
            Arg.Any<CancellationToken>());

        await sut.Handle(cmd, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.CreatedAt.Should().Be(_fixedNow,
            because: "created_at deve vir do IClock, não do chamador (REQ-002.4)");
    }

    // -----------------------------------------------------------------------
    // TenantId derivado do contexto
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Handler deve derivar TenantId do ITenantContext")]
    public async Task Handle_Should_Use_TenantId_From_Context()
    {
        var sut = BuildSut();
        var cmd = ValidCreateCommand();

        AuditLogAggregate? captured = null;
        await _repository.AddAsync(
            Arg.Do<AuditLogAggregate>(a => captured = a),
            Arg.Any<CancellationToken>());

        await sut.Handle(cmd, CancellationToken.None);

        captured!.TenantId.Value.Should().Be(_tenantIdValue,
            because: "TenantId deve vir do ITenantContext, não do command (REQ-005.3)");
    }

    [Fact(DisplayName = "Handler deve lançar InvalidOperationException quando TenantId está ausente")]
    public async Task Handle_Should_Throw_When_TenantId_Missing()
    {
        _tenantContext.TenantId.Returns((Guid?)null);
        var sut = BuildSut();
        var cmd = ValidCreateCommand();

        var act = async () => await sut.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "TenantId ausente indica falha de configuração");
    }

    // -----------------------------------------------------------------------
    // Mascaramento de PII antes da persistência
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Handler deve aplicar mascaramento de PII antes de persistir")]
    public async Task Handle_Should_Apply_Pii_Masking_Before_Persisting()
    {
        var piiPolicy = BuildContactPiiPolicy();
        var sut = BuildSut(piiPolicy);

        var rawEmail = "joao@teste.com";
        var cmd = new RecordAuditEntryCommand
        {
            ActorId = Guid.NewGuid(),
            EntityType = "Contact",
            EntityId = Guid.NewGuid(),
            Action = AuditAction.Create,
            RawBefore = null,
            RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = "João Silva",
                ["email"] = rawEmail,
                ["phone"] = "11999998888"
            }
        };

        AuditLogAggregate? captured = null;
        await _repository.AddAsync(
            Arg.Do<AuditLogAggregate>(a => captured = a),
            Arg.Any<CancellationToken>());

        await sut.Handle(cmd, CancellationToken.None);

        captured.Should().NotBeNull();
        var deltaAfter = captured!.Delta.After!;

        deltaAfter["email"].Should().Be(PiiMasker.MaskedMarker,
            because: "email é campo PII e deve ser mascarado antes de persistir (REQ-004)");
        deltaAfter["name"].Should().Be(PiiMasker.MaskedMarker,
            because: "name é campo PII para Contact e deve ser mascarado (REQ-004)");
        deltaAfter["phone"].Should().Be(PiiMasker.MaskedMarker,
            because: "phone é campo PII para Contact e deve ser mascarado (REQ-004)");
    }

    [Fact(DisplayName = "Handler deve persistir delta sem PII em texto claro")]
    public async Task Handle_Should_Not_Persist_Raw_Pii()
    {
        var piiPolicy = BuildContactPiiPolicy();
        var sut = BuildSut(piiPolicy);

        var cmd = new RecordAuditEntryCommand
        {
            ActorId = Guid.NewGuid(),
            EntityType = "Contact",
            EntityId = Guid.NewGuid(),
            Action = AuditAction.Create,
            RawBefore = null,
            RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["email"] = "pii@original.com"
            }
        };

        AuditLogAggregate? captured = null;
        await _repository.AddAsync(
            Arg.Do<AuditLogAggregate>(a => captured = a),
            Arg.Any<CancellationToken>());

        await sut.Handle(cmd, CancellationToken.None);

        // Nenhum valor de PII original deve aparecer no delta persistido
        var serialized = System.Text.Json.JsonSerializer.Serialize(captured!.Delta.After);
        serialized.Should().NotContain("pii@original.com",
            because: "PII original não deve aparecer em texto claro no delta persistido (REQ-004.2)");
    }

    // -----------------------------------------------------------------------
    // Persistência via repositório
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Handler deve chamar repositório exatamente uma vez")]
    public async Task Handle_Should_Call_Repository_Once()
    {
        var sut = BuildSut();
        await sut.Handle(ValidCreateCommand(), CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Any<AuditLogAggregate>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handler deve incrementar contador de eventos recebidos")]
    public async Task Handle_Should_Increment_Events_Received_Metric()
    {
        var sut = BuildSut();
        await sut.Handle(ValidCreateCommand(), CancellationToken.None);

        _metrics.Received(1).IncrementEventsReceived();
    }

    [Fact(DisplayName = "Handler deve incrementar contador de falhas e propagar exceção quando repositório falha")]
    public async Task Handle_Should_Increment_Failure_And_Rethrow_When_Repository_Fails()
    {
        var sut = BuildSut();
        _repository
            .AddAsync(Arg.Any<AuditLogAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Falha simulada")));

        var act = async () => await sut.Handle(ValidCreateCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _metrics.Received(1).IncrementInsertFailures();
    }

    // -----------------------------------------------------------------------
    // Delta correctness
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Handler deve construir delta ForCreate com RawAfter")]
    public async Task Handle_Create_Should_Build_Delta_With_After()
    {
        var sut = BuildSut();
        var afterData = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Test" };
        var cmd = ValidCreateCommand() with { RawAfter = afterData };

        AuditLogAggregate? captured = null;
        await _repository.AddAsync(
            Arg.Do<AuditLogAggregate>(a => captured = a),
            Arg.Any<CancellationToken>());

        await sut.Handle(cmd, CancellationToken.None);

        captured!.Delta.Kind.Should().Be(AuditDeltaKind.Create);
        captured.Delta.After.Should().NotBeNull();
    }

    [Fact(DisplayName = "Handler deve construir delta ForDelete com RawBefore")]
    public async Task Handle_Delete_Should_Build_Delta_With_Before()
    {
        var sut = BuildSut();
        var cmd = ValidDeleteCommand();

        AuditLogAggregate? captured = null;
        await _repository.AddAsync(
            Arg.Do<AuditLogAggregate>(a => captured = a),
            Arg.Any<CancellationToken>());

        await sut.Handle(cmd, CancellationToken.None);

        captured!.Delta.Kind.Should().Be(AuditDeltaKind.Delete);
        captured.Delta.Before.Should().NotBeNull();
    }

    [Fact(DisplayName = "Handler deve construir delta ForUpdate apenas com campos alterados")]
    public async Task Handle_Update_Should_Build_Delta_With_Changes_Only()
    {
        var sut = BuildSut();
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();

        var cmd = new RecordAuditEntryCommand
        {
            ActorId = Guid.NewGuid(),
            EntityType = "Opportunity",
            EntityId = Guid.NewGuid(),
            Action = AuditAction.Update,
            RawBefore = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["stage_id"] = oldId,
                ["name"] = "Oportunidade"
            },
            RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["stage_id"] = newId,
                ["name"] = "Oportunidade" // sem mudança
            }
        };

        AuditLogAggregate? captured = null;
        await _repository.AddAsync(
            Arg.Do<AuditLogAggregate>(a => captured = a),
            Arg.Any<CancellationToken>());

        await sut.Handle(cmd, CancellationToken.None);

        captured!.Delta.Kind.Should().Be(AuditDeltaKind.Update);
        captured.Delta.Changes.Should().HaveCount(1, because: "somente stage_id foi alterado");
        captured.Delta.Changes!.Should().ContainKey("stage_id");
        captured.Delta.Changes!.Should().NotContainKey("name");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static RecordAuditEntryCommand ValidCreateCommand() => new()
    {
        ActorId = Guid.NewGuid(),
        EntityType = "Opportunity",
        EntityId = Guid.NewGuid(),
        Action = AuditAction.Create,
        RawBefore = null,
        RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Test" }
    };

    private static RecordAuditEntryCommand ValidDeleteCommand() => new()
    {
        ActorId = Guid.NewGuid(),
        EntityType = "Opportunity",
        EntityId = Guid.NewGuid(),
        Action = AuditAction.Delete,
        RawBefore = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Test" },
        RawAfter = null
    };

    private static IPiiFieldPolicy BuildNoPiiPolicy()
    {
        var policy = Substitute.For<IPiiFieldPolicy>();
        policy.GetPiiFields(Arg.Any<string>())
              .Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return policy;
    }

    private static IPiiFieldPolicy BuildContactPiiPolicy()
    {
        var policy = Substitute.For<IPiiFieldPolicy>();
        policy.GetPiiFields("Contact")
              .Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "name", "email", "phone" });
        policy.GetPiiFields(Arg.Is<string>(s => s != "Contact"))
              .Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return policy;
    }
}
