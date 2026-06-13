using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TenantAdministration.Application.Behaviors;
using TenantAdministration.Application.Ports;
using Xunit;

namespace TenantAdministration.Application.Tests.Behaviors;

public sealed class TransactionBehaviorTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private TransactionBehavior<TRequest, TResponse> CreateBehavior<TRequest, TResponse>()
        where TRequest : notnull =>
        new(_unitOfWork, _tenantContext,
            NullLogger<TransactionBehavior<TRequest, TResponse>>.Instance);

    [Fact(DisplayName = "Handler bem-sucedido: Begin + Commit chamados; Rollback não")]
    public async Task SuccessfulHandler_ShouldBeginAndCommit()
    {
        _tenantContext.TenantId.Returns(Guid.NewGuid());
        var behavior = CreateBehavior<SampleCommand, string>();

        var result = await behavior.Handle(
            new SampleCommand(),
            ct => Task.FromResult("ok"),
            CancellationToken.None);

        result.Should().Be("ok");
        await _unitOfWork.Received(1).BeginAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handler com exceção: Rollback é chamado e exceção é relançada")]
    public async Task FailingHandler_ShouldRollback_AndRethrow()
    {
        _tenantContext.TenantId.Returns((Guid?)null);
        var behavior = CreateBehavior<SampleCommand, string>();

        var act = () => behavior.Handle(
            new SampleCommand(),
            ct => throw new InvalidOperationException("erro no handler"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem tenant_id: Begin é chamado com null (plano de plataforma)")]
    public async Task NullTenantId_BeginCalledWithNull()
    {
        _tenantContext.TenantId.Returns((Guid?)null);
        var behavior = CreateBehavior<SampleCommand, string>();

        await behavior.Handle(new SampleCommand(), ct => Task.FromResult("ok"), CancellationToken.None);

        await _unitOfWork.Received(1).BeginAsync(null, Arg.Any<CancellationToken>());
    }

    private sealed record SampleCommand : IRequest<string>;
}
