using FluentAssertions;
using MediatR;
using NSubstitute;
using TenantAdministration.Application.Authorization;
using TenantAdministration.Application.Behaviors;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;
using Xunit;

namespace TenantAdministration.Application.Tests.Behaviors;

public sealed class AuthorizationBehaviorTests
{
    private readonly ICurrentUserContext _userContext = Substitute.For<ICurrentUserContext>();
    private readonly RequestHandlerDelegate<string> _next = ct => Task.FromResult("ok");

    private AuthorizationBehavior<TRequest, string> CreateBehavior<TRequest>()
        where TRequest : notnull =>
        new(_userContext);

    // ──────────────────────────────────────────────────────────────
    // Commands de plataforma: bloqueados para não-PlatOp
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "PlatOp command: TAdmin deve receber AuthorizationException")]
    public async Task PlatOpCommand_TenantAdmin_ShouldThrow()
    {
        _userContext.IsPlatformOperator.Returns(false);
        _userContext.IsTenantAdmin.Returns(true);
        _userContext.IsViewer.Returns(false);

        var behavior = CreateBehavior<PlatOpCommand>();
        var act = () => behavior.Handle(new PlatOpCommand(), _next, CancellationToken.None);

        await act.Should().ThrowAsync<AuthorizationException>()
            .WithMessage("*Platform Operator*");
    }

    [Fact(DisplayName = "PlatOp command: PlatOp deve passar")]
    public async Task PlatOpCommand_PlatformOperator_ShouldSucceed()
    {
        _userContext.IsPlatformOperator.Returns(true);

        var behavior = CreateBehavior<PlatOpCommand>();
        var result = await behavior.Handle(new PlatOpCommand(), _next, CancellationToken.None);

        result.Should().Be("ok");
    }

    // ──────────────────────────────────────────────────────────────
    // Commands de tenant: bloqueados para PlatOp
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TAdmin command: PlatOp deve receber AuthorizationException (RNF 7)")]
    public async Task TAdminCommand_PlatformOperator_ShouldThrow()
    {
        _userContext.IsPlatformOperator.Returns(true);
        _userContext.IsTenantAdmin.Returns(false);

        var behavior = CreateBehavior<TAdminCommand>();
        var act = () => behavior.Handle(new TAdminCommand(), _next, CancellationToken.None);

        await act.Should().ThrowAsync<AuthorizationException>()
            .WithMessage("*Platform Operator não pode*");
    }

    [Fact(DisplayName = "TAdmin command: TAdmin sem papel não passa")]
    public async Task TAdminCommand_NonAdmin_ShouldThrow()
    {
        _userContext.IsPlatformOperator.Returns(false);
        _userContext.IsTenantAdmin.Returns(false);
        _userContext.IsViewer.Returns(false);

        var behavior = CreateBehavior<TAdminCommand>();
        var act = () => behavior.Handle(new TAdminCommand(), _next, CancellationToken.None);

        await act.Should().ThrowAsync<AuthorizationException>()
            .WithMessage("*Tenant Admin*");
    }

    [Fact(DisplayName = "TAdmin command: TAdmin deve passar")]
    public async Task TAdminCommand_TenantAdmin_ShouldSucceed()
    {
        _userContext.IsPlatformOperator.Returns(false);
        _userContext.IsTenantAdmin.Returns(true);

        var behavior = CreateBehavior<TAdminCommand>();
        var result = await behavior.Handle(new TAdminCommand(), _next, CancellationToken.None);

        result.Should().Be("ok");
    }

    // ──────────────────────────────────────────────────────────────
    // Commands anônimos (AllowAnonymous): qualquer caller passa
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "AllowAnonymous command: qualquer chamador passa sem verificação")]
    public async Task AllowAnonymousCommand_AnyCaller_ShouldPass()
    {
        _userContext.IsPlatformOperator.Returns(false);
        _userContext.IsTenantAdmin.Returns(false);
        _userContext.IsViewer.Returns(false);

        var behavior = CreateBehavior<AnonCommand>();
        var result = await behavior.Handle(new AnonCommand(), _next, CancellationToken.None);

        result.Should().Be("ok");
    }

    // ──────────────────────────────────────────────────────────────
    // Tipos de teste (stubs de command com atributos)
    // ──────────────────────────────────────────────────────────────

    [RequiresPlatformOperator]
    private sealed record PlatOpCommand : IRequest<string>;

    [RequiresTenantAdmin]
    private sealed record TAdminCommand : IRequest<string>;

    [AllowAnonymous]
    private sealed record AnonCommand : IRequest<string>;
}
