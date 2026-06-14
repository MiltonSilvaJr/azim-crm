namespace ActivityManagement.Application.Tests.Behaviors;

using ActivityManagement.Application.Behaviors;
using ActivityManagement.Domain.Activities.Events;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Testes unitários do <see cref="TransactionBehavior{TRequest,TResponse}"/>.
/// Verifica: Commands transacionais disparam CommitAsync; Queries passam sem CommitAsync;
/// Domain events são passados para CommitAsync.
/// Mapeia: design §5.4, §6.5, TASK-06.
/// </summary>
public sealed class TransactionBehaviorTests
{
    private sealed record TransactionalCommand : IRequest<string>, ITransactionalCommand
    {
        public IReadOnlyList<DomainEvent> DomainEvents { get; init; } = [];
    }

    private sealed record NonTransactionalQuery : IRequest<string>
    {
    }

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_TransactionalCommand_CallsCommitAsync()
    {
        var behavior = new TransactionBehavior<TransactionalCommand, string>(
            _unitOfWork,
            NullLogger<TransactionBehavior<TransactionalCommand, string>>.Instance);
        var request  = new TransactionalCommand();

        await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

        await _unitOfWork.Received(1).CommitAsync(
            Arg.Any<IReadOnlyList<DomainEvent>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonTransactionalQuery_DoesNotCallCommitAsync()
    {
        var behavior = new TransactionBehavior<NonTransactionalQuery, string>(
            _unitOfWork,
            NullLogger<TransactionBehavior<NonTransactionalQuery, string>>.Instance);
        var request  = new NonTransactionalQuery();

        await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

        await _unitOfWork.DidNotReceive().CommitAsync(
            Arg.Any<IReadOnlyList<DomainEvent>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TransactionalCommand_PassesDomainEventsToCommit()
    {
        var domainEvent = new ActivityCreated(
            EventId:       Guid.NewGuid(),
            OccurredAt:    DateTimeOffset.UtcNow,
            ActivityId:    Guid.NewGuid(),
            TenantId:      Guid.NewGuid(),
            BuId:          Guid.NewGuid(),
            OwnerId:       Guid.NewGuid(),
            Type:          "meeting",
            DueAt:         DateTimeOffset.UtcNow.AddDays(1),
            CorrelationId: Guid.NewGuid(),
            OpportunityId: null,
            AccountId:     null);

        var behavior = new TransactionBehavior<TransactionalCommand, string>(
            _unitOfWork,
            NullLogger<TransactionBehavior<TransactionalCommand, string>>.Instance);
        var request  = new TransactionalCommand { DomainEvents = [domainEvent] };

        await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

        await _unitOfWork.Received(1).CommitAsync(
            Arg.Is<IReadOnlyList<DomainEvent>>(events => events.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TransactionalCommand_ReturnsHandlerResponse()
    {
        var behavior = new TransactionBehavior<TransactionalCommand, string>(
            _unitOfWork,
            NullLogger<TransactionBehavior<TransactionalCommand, string>>.Instance);
        var request  = new TransactionalCommand();

        var response = await behavior.Handle(request, _ => Task.FromResult("resultado"), CancellationToken.None);

        response.Should().Be("resultado");
    }
}
