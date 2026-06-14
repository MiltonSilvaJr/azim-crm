using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Organization.Application.Abstractions;
using Organization.Application.Behaviors;
using Organization.Application.Ports;
using Xunit;

namespace Organization.Application.Tests.Behaviors;

/// <summary>
/// Testes unitários para <see cref="TransactionBehavior{TRequest,TResponse}"/>.
/// </summary>
public sealed class TransactionBehaviorTests
{
    private readonly IDatabaseContext _databaseContext = Substitute.For<IDatabaseContext>();

    private TransactionBehavior<TRequest, string> CreateBehavior<TRequest>()
        where TRequest : notnull
        => new(_databaseContext, NullLogger<TransactionBehavior<TRequest, string>>.Instance);

    [Fact]
    public async Task Handle_Command_BeginsAndCommitsTransaction()
    {
        // Arrange
        var behavior = CreateBehavior<FakeCommand>();

        // Act
        var result = await behavior.Handle(
            new FakeCommand(),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert
        result.Should().Be("ok");
        await _databaseContext.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _databaseContext.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
        await _databaseContext.DidNotReceive().RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CommandFails_RollsBackTransaction()
    {
        // Arrange
        var behavior = CreateBehavior<FakeCommand>();

        // Act
        var act = () => behavior.Handle(
            new FakeCommand(),
            _ => throw new InvalidOperationException("falha no handler"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _databaseContext.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _databaseContext.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
        await _databaseContext.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Query_DoesNotUseTransaction()
    {
        // Arrange
        var behavior = new TransactionBehavior<FakeQuery, string>(
            _databaseContext,
            NullLogger<TransactionBehavior<FakeQuery, string>>.Instance);

        // Act
        var result = await behavior.Handle(
            new FakeQuery(),
            _ => Task.FromResult("resultado query"),
            CancellationToken.None);

        // Assert
        result.Should().Be("resultado query");
        await _databaseContext.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _databaseContext.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OutboxWrittenBeforeCommit_AtomicityGuaranteed()
    {
        // Arrange
        var callOrder = new List<string>();
        _databaseContext.BeginTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("Begin"); return Task.CompletedTask; });
        _databaseContext.CommitTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("Commit"); return Task.CompletedTask; });

        var behavior = CreateBehavior<FakeCommand>();

        // Act
        await behavior.Handle(
            new FakeCommand(),
            _ => { callOrder.Add("Handler"); return Task.FromResult("ok"); },
            CancellationToken.None);

        // Assert
        callOrder.Should().ContainInOrder("Begin", "Handler", "Commit");
    }

    // ── Stubs ──

    private sealed record FakeCommand : ICommand<string>;
    private sealed record FakeQuery : IQuery<string>;
}
