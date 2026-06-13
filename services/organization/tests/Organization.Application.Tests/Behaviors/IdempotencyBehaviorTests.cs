using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Organization.Application.Behaviors;
using Organization.Application.Ports;
using Xunit;

namespace Organization.Application.Tests.Behaviors;

/// <summary>
/// Testes unitários para <see cref="IdempotencyBehavior{TRequest,TResponse}"/>.
/// </summary>
public sealed class IdempotencyBehaviorTests
{
    private readonly IInboxStore _inboxStore = Substitute.For<IInboxStore>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private IdempotencyBehavior<TRequest, string> CreateBehavior<TRequest>()
        where TRequest : notnull
        => new(_inboxStore, _tenantContext, NullLogger<IdempotencyBehavior<TRequest, string>>.Instance);

    [Fact]
    public async Task Handle_NonIdempotentRequest_CallsNext()
    {
        // Arrange — request sem IIdempotentRequest = passa direto
        var behavior = CreateBehavior<SimpleRequest>();
        var nextCalled = false;

        // Act
        await behavior.Handle(
            new SimpleRequest(),
            _ => { nextCalled = true; return Task.FromResult("ok"); },
            CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
        await _inboxStore.DidNotReceive().IsProcessedAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_IdempotentRequest_AlreadyProcessed_SkipsHandler()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);
        _inboxStore.IsProcessedAsync("token-abc", tenantId, Arg.Any<CancellationToken>())
            .Returns(true);

        var behavior = CreateBehavior<IdempotentRequest>();
        var nextCalled = false;

        // Act
        var result = await behavior.Handle(
            new IdempotentRequest("token-abc"),
            _ => { nextCalled = true; return Task.FromResult("executado"); },
            CancellationToken.None);

        // Assert
        nextCalled.Should().BeFalse("segundo processamento não deve executar o handler");
        result.Should().BeNull("resultado default para idempotência");
    }

    [Fact]
    public async Task Handle_IdempotentRequest_FirstTime_CallsHandlerAndMarksProcessed()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);
        _inboxStore.IsProcessedAsync("token-xyz", tenantId, Arg.Any<CancellationToken>())
            .Returns(false);

        var behavior = CreateBehavior<IdempotentRequest>();

        // Act
        var result = await behavior.Handle(
            new IdempotentRequest("token-xyz"),
            _ => Task.FromResult("processado"),
            CancellationToken.None);

        // Assert
        result.Should().Be("processado");
        await _inboxStore.Received(1).MarkProcessedAsync(
            "token-xyz",
            tenantId,
            "invitation.accept",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_IdempotentRequest_MultipleReprocessings_AllSkipHandler()
    {
        // Arrange — PBT-03 simulation: N reprocessamentos retornam sem executar
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);
        _inboxStore.IsProcessedAsync(Arg.Any<string>(), tenantId, Arg.Any<CancellationToken>())
            .Returns(true);

        var behavior = CreateBehavior<IdempotentRequest>();
        var executionCount = 0;

        // Act — simula 5 reprocessamentos
        for (var i = 0; i < 5; i++)
        {
            await behavior.Handle(
                new IdempotentRequest("same-key"),
                _ => { executionCount++; return Task.FromResult("ok"); },
                CancellationToken.None);
        }

        // Assert
        executionCount.Should().Be(0, "nenhum reprocessamento deve executar o handler");
    }

    // ── Stubs ──

    private sealed record SimpleRequest;

    private sealed record IdempotentRequest(string Key) : IIdempotentRequest
    {
        public string IdempotencyKey => Key;
        public string EventType => "invitation.accept";
    }
}
