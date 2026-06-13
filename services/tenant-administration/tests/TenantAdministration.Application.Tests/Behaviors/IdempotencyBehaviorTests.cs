using FluentAssertions;
using MediatR;
using NSubstitute;
using TenantAdministration.Application.Behaviors;
using TenantAdministration.Application.Ports;
using Xunit;

namespace TenantAdministration.Application.Tests.Behaviors;

public sealed class IdempotencyBehaviorTests
{
    private readonly IIdempotencyStore _store = Substitute.For<IIdempotencyStore>();

    private IdempotencyBehavior<TRequest, TResponse> CreateBehavior<TRequest, TResponse>()
        where TRequest : notnull =>
        new(_store);

    [Fact(DisplayName = "Command sem IdempotencyKey: handler é chamado normalmente")]
    public async Task NoIdempotencyKey_ShouldCallHandler()
    {
        var handlerCalled = false;
        RequestHandlerDelegate<string> next = ct =>
        {
            handlerCalled = true;
            return Task.FromResult("resultado");
        };

        var behavior = CreateBehavior<NoKeyCommand, string>();
        await behavior.Handle(new NoKeyCommand(), next, CancellationToken.None);

        handlerCalled.Should().BeTrue();
        await _store.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Primeira chamada com chave: handler é executado e resultado é armazenado")]
    public async Task FirstCall_WithKey_ShouldExecuteAndStore()
    {
        _store.GetAsync("key-abc", Arg.Any<CancellationToken>()).Returns((string?)null);

        var behavior = CreateBehavior<KeyCommand, string>();
        var result = await behavior.Handle(
            new KeyCommand("key-abc"),
            ct => Task.FromResult("resultado-1"),
            CancellationToken.None);

        result.Should().Be("resultado-1");
        await _store.Received(1).SetAsync(
            "key-abc",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Segunda chamada com mesma chave: resultado cacheado é retornado sem chamar handler")]
    public async Task SecondCall_SameKey_ShouldReturnCachedResult_WithoutCallingHandler()
    {
        // Serialização de "resultado-1" para simular armazenamento prévio
        var cachedJson = System.Text.Json.JsonSerializer.Serialize("resultado-1");
        _store.GetAsync("key-abc", Arg.Any<CancellationToken>()).Returns(cachedJson);

        var handlerCalled = false;
        RequestHandlerDelegate<string> next = ct =>
        {
            handlerCalled = true;
            return Task.FromResult("resultado-2");
        };

        var behavior = CreateBehavior<KeyCommand, string>();
        var result = await behavior.Handle(new KeyCommand("key-abc"), next, CancellationToken.None);

        result.Should().Be("resultado-1");
        handlerCalled.Should().BeFalse("handler não deve ser chamado quando há resultado cacheado");
        await _store.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────────────────────
    // Stubs
    // ──────────────────────────────────────────────────────────────

    private sealed record NoKeyCommand : IRequest<string>;

    private sealed record KeyCommand(string? IdempotencyKey) : IRequest<string>, IIdempotentCommand;
}
