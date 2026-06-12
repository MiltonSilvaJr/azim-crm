using AuditLog.Application.Abstractions;
using AuditLog.Application.Behaviors;
using AuditLog.Application.Commands;
using AuditLog.Application.Errors;
using AuditLog.Application.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AuditLog.Application.Tests.Behaviors;

/// <summary>
/// Testes unitários do <see cref="TenantContextBehavior{TRequest,TResponse}"/>.
/// Verifica bloqueio de requisições sem tenant_id e incremento de contador (DD-007).
/// </summary>
public sealed class TenantContextBehaviorTests
{
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IAuditMetrics _metrics = Substitute.For<IAuditMetrics>();

    private TenantContextBehavior<ListAuditLogsQuery, Unit> BuildQuerySut() =>
        new(_tenantContext, _metrics,
            NullLogger<TenantContextBehavior<ListAuditLogsQuery, Unit>>.Instance);

    private TenantContextBehavior<RecordAuditEntryCommand, Unit> BuildCommandSut() =>
        new(_tenantContext, _metrics,
            NullLogger<TenantContextBehavior<RecordAuditEntryCommand, Unit>>.Instance);

    // -----------------------------------------------------------------------
    // Bloqueio sem tenant_id (AUD-ERR-008)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Query sem tenant_id deve lançar AuditAuthorizationException (AUD-ERR-008)")]
    public async Task Query_Without_TenantId_Should_Throw_AuthorizationException()
    {
        _tenantContext.TenantId.Returns((Guid?)null);
        var sut = BuildQuerySut();

        var act = async () => await sut.Handle(
            new ListAuditLogsQuery(),
            _ => Task.FromResult(Unit.Value),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AuditAuthorizationException>();
        ex.Which.ErrorCode.Should().Be(AuditErrorCodes.TenantContextMissing,
            because: "ausência de tenant_id resulta em AUD-ERR-008");
    }

    [Fact(DisplayName = "Command sem tenant_id deve lançar AuditAuthorizationException (AUD-ERR-008)")]
    public async Task Command_Without_TenantId_Should_Throw_AuthorizationException()
    {
        _tenantContext.TenantId.Returns((Guid?)null);
        var sut = BuildCommandSut();

        var act = async () => await sut.Handle(
            ValidCommand(),
            _ => Task.FromResult(Unit.Value),
            CancellationToken.None);

        await act.Should().ThrowAsync<AuditAuthorizationException>();
    }

    // -----------------------------------------------------------------------
    // Incremento de contador (DD-007)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve incrementar contador audit_query_without_tenant_context_total quando tenant_id ausente")]
    public async Task Should_Increment_Counter_When_TenantId_Missing()
    {
        _tenantContext.TenantId.Returns((Guid?)null);
        var sut = BuildQuerySut();

        try
        {
            await sut.Handle(
                new ListAuditLogsQuery(),
                _ => Task.FromResult(Unit.Value),
                CancellationToken.None);
        }
        catch (AuditAuthorizationException) { /* esperado */ }

        _metrics.Received(1).IncrementQueryWithoutTenantContext();
    }

    [Fact(DisplayName = "NÃO deve incrementar contador quando tenant_id está presente")]
    public async Task Should_Not_Increment_Counter_When_TenantId_Present()
    {
        _tenantContext.TenantId.Returns(Guid.NewGuid());
        var sut = BuildQuerySut();

        await sut.Handle(
            new ListAuditLogsQuery(),
            _ => Task.FromResult(Unit.Value),
            CancellationToken.None);

        _metrics.DidNotReceive().IncrementQueryWithoutTenantContext();
    }

    // -----------------------------------------------------------------------
    // Passa para o handler quando tenant_id está presente
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve chamar o próximo handler quando tenant_id está presente")]
    public async Task Should_Call_Next_When_TenantId_Present()
    {
        _tenantContext.TenantId.Returns(Guid.NewGuid());
        var sut = BuildQuerySut();

        var nextCalled = false;
        await sut.Handle(
            new ListAuditLogsQuery(),
            _ =>
            {
                nextCalled = true;
                return Task.FromResult(Unit.Value);
            },
            CancellationToken.None);

        nextCalled.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static RecordAuditEntryCommand ValidCommand() => new()
    {
        ActorId = Guid.NewGuid(),
        EntityType = "Opportunity",
        EntityId = Guid.NewGuid(),
        Action = Domain.ValueObjects.AuditAction.Create,
        RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "T" }
    };
}
