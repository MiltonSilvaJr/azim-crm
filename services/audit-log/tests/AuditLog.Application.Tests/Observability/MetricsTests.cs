using AuditLog.Application.Abstractions;
using AuditLog.Application.Commands;
using AuditLog.Application.Handlers;
using AuditLog.Domain.Abstractions;
using AuditLog.Domain.Repositories;
using AuditLog.Domain.Services;
using AuditLog.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace AuditLog.Application.Tests.Observability;

/// <summary>
/// Testes unitários dos contadores de métricas do AuditService (design §11.2, RNF-004).
/// Verifica que os contadores corretos são incrementados em cada cenário.
/// </summary>
public sealed class MetricsTests
{
    private readonly IAuditLogRepository _repository = Substitute.For<IAuditLogRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IAuditMetrics _metrics = Substitute.For<IAuditMetrics>();
    private readonly PiiMasker _piiMasker;
    private readonly IPiiFieldPolicy _noPiiPolicy;

    public MetricsTests()
    {
        _clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        _tenantContext.TenantId.Returns(Guid.NewGuid());

        _noPiiPolicy = new PiiFieldPolicy(new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase));
        _piiMasker = new PiiMasker(_noPiiPolicy);
    }

    private AuditService BuildSut() =>
        new(_repository, _piiMasker, _noPiiPolicy, _clock, _tenantContext, _metrics,
            NullLogger<AuditService>.Instance);

    // -----------------------------------------------------------------------
    // audit_events_received_total
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve incrementar audit_events_received_total ao processar comando com sucesso")]
    public async Task Should_Increment_EventsReceived_On_Success()
    {
        var sut = BuildSut();

        await sut.Handle(ValidCommand(), CancellationToken.None);

        _metrics.Received(1).IncrementEventsReceived();
    }

    [Fact(DisplayName = "Deve incrementar audit_events_received_total mesmo quando INSERT falha")]
    public async Task Should_Increment_EventsReceived_Even_When_Insert_Fails()
    {
        _repository.AddAsync(Arg.Any<Domain.Aggregates.AuditLogAggregate>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Falha simulada no banco"));

        var sut = BuildSut();

        try { await sut.Handle(ValidCommand(), CancellationToken.None); }
        catch (InvalidOperationException) { /* esperado */ }

        _metrics.Received(1).IncrementEventsReceived();
    }

    // -----------------------------------------------------------------------
    // audit_insert_failures_total
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve incrementar audit_insert_failures_total quando INSERT falha")]
    public async Task Should_Increment_InsertFailures_When_Repo_Throws()
    {
        _repository.AddAsync(Arg.Any<Domain.Aggregates.AuditLogAggregate>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Falha simulada no banco"));

        var sut = BuildSut();

        try { await sut.Handle(ValidCommand(), CancellationToken.None); }
        catch (InvalidOperationException) { /* esperado */ }

        _metrics.Received(1).IncrementInsertFailures();
    }

    [Fact(DisplayName = "NÃO deve incrementar audit_insert_failures_total quando INSERT tem sucesso")]
    public async Task Should_Not_Increment_InsertFailures_On_Success()
    {
        var sut = BuildSut();

        await sut.Handle(ValidCommand(), CancellationToken.None);

        _metrics.DidNotReceive().IncrementInsertFailures();
    }

    // -----------------------------------------------------------------------
    // audit_insert_latency_seconds
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve registrar latência de INSERT (audit_insert_latency_seconds) em operação bem-sucedida")]
    public async Task Should_Record_InsertLatency_On_Success()
    {
        var sut = BuildSut();

        await sut.Handle(ValidCommand(), CancellationToken.None);

        _metrics.Received(1).RecordInsertLatency(Arg.Any<double>());
    }

    [Fact(DisplayName = "Deve registrar latência de INSERT mesmo quando INSERT falha")]
    public async Task Should_Record_InsertLatency_Even_On_Failure()
    {
        _repository.AddAsync(Arg.Any<Domain.Aggregates.AuditLogAggregate>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Falha simulada"));

        var sut = BuildSut();

        try { await sut.Handle(ValidCommand(), CancellationToken.None); }
        catch (InvalidOperationException) { /* esperado */ }

        _metrics.Received(1).RecordInsertLatency(Arg.Any<double>());
    }

    // -----------------------------------------------------------------------
    // audit_pii_masking_applied_total
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve incrementar audit_pii_masking_applied_total quando mascaramento é aplicado")]
    public async Task Should_Increment_PiiMasking_When_Masking_Applied()
    {
        // Usa entidade com campos PII configurados
        var policyWithPii = new PiiFieldPolicy(new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Contact"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "email", "phone" }
        });
        var maskerWithPolicy = new PiiMasker(policyWithPii);

        var sut = new AuditService(_repository, maskerWithPolicy, policyWithPii, _clock, _tenantContext, _metrics,
            NullLogger<AuditService>.Instance);

        var commandWithPii = new RecordAuditEntryCommand
        {
            ActorId = Guid.NewGuid(),
            EntityType = "Contact",
            EntityId = Guid.NewGuid(),
            Action = AuditAction.Create,
            RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["email"] = "usuario@exemplo.com",
                ["phone"] = "(11) 99999-9999"
            }
        };

        await sut.Handle(commandWithPii, CancellationToken.None);

        _metrics.Received(1).IncrementPiiMaskingApplied();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static RecordAuditEntryCommand ValidCommand() => new()
    {
        ActorId = Guid.NewGuid(),
        EntityType = "Opportunity",
        EntityId = Guid.NewGuid(),
        Action = AuditAction.Create,
        RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Oportunidade X" }
    };
}
