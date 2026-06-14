using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Organization.Application.Abstractions;
using Organization.Application.Behaviors;
using Organization.Application.Ports;
using Xunit;

namespace Organization.Application.Tests.Behaviors;

/// <summary>
/// Testes unitários para <see cref="RbacAuthorizationBehavior{TRequest,TResponse}"/>.
/// </summary>
public sealed class RbacAuthorizationBehaviorTests
{
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private RbacAuthorizationBehavior<TRequest, string> CreateBehavior<TRequest>()
        where TRequest : notnull
        => new(_tenantContext, NullLogger<RbacAuthorizationBehavior<TRequest, string>>.Instance);

    [Fact]
    public async Task Handle_RequestWithoutAttribute_DeniesAccess()
    {
        // Arrange — sem RequiresRoleAttribute = deny-by-default
        _tenantContext.TenantId.Returns(Guid.NewGuid());
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string> { [Guid.NewGuid()] = "TAdmin" });

        var behavior = CreateBehavior<RequestWithoutAttribute>();

        // Act
        var act = () => behavior.Handle(
            new RequestWithoutAttribute(),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*ORG-ERR-010*");
    }

    [Fact]
    public async Task Handle_RequestWithWrongRole_DeniesAccess()
    {
        // Arrange — usuário tem Vendedor, mas o request requer TAdmin
        _tenantContext.TenantId.Returns(Guid.NewGuid());
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string> { [Guid.NewGuid()] = "Vendedor" });

        var behavior = CreateBehavior<RequestRequiringTAdmin>();

        // Act
        var act = () => behavior.Handle(
            new RequestRequiringTAdmin(),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*ORG-ERR-010*");
    }

    [Fact]
    public async Task Handle_RequestWithCorrectRole_AllowsAccess()
    {
        // Arrange — usuário tem TAdmin = autorizado
        _tenantContext.TenantId.Returns(Guid.NewGuid());
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string> { [Guid.NewGuid()] = "TAdmin" });

        var behavior = CreateBehavior<RequestRequiringTAdmin>();

        // Act
        var result = await behavior.Handle(
            new RequestRequiringTAdmin(),
            _ => Task.FromResult("autorizado"),
            CancellationToken.None);

        // Assert
        result.Should().Be("autorizado");
    }

    [Fact]
    public async Task Handle_AllowAnonymousRequest_SkipsRbacCheck()
    {
        // Arrange — AllowAnonymous = sem verificação RBAC (aceite de convite)
        _tenantContext.TenantId.Returns(Guid.Empty);
        _tenantContext.UserId.Returns(Guid.Empty);
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());

        var behavior = CreateBehavior<AnonymousRequest>();

        // Act
        var result = await behavior.Handle(
            new AnonymousRequest(),
            _ => Task.FromResult("aceito"),
            CancellationToken.None);

        // Assert
        result.Should().Be("aceito");
    }

    [Fact]
    public async Task Handle_UserWithMultipleRoles_AllowsIfAnyMatches()
    {
        // Arrange — usuário tem GestorBU em uma BU e TAdmin em outra
        _tenantContext.TenantId.Returns(Guid.NewGuid());
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>
        {
            [Guid.NewGuid()] = "GestorBU",
            [Guid.NewGuid()] = "TAdmin"
        });

        var behavior = CreateBehavior<RequestRequiringTAdmin>();

        // Act
        var result = await behavior.Handle(
            new RequestRequiringTAdmin(),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert
        result.Should().Be("ok");
    }

    // ── Stubs de request para os testes ──

    private sealed record RequestWithoutAttribute;

    [RequiresRole("TAdmin")]
    private sealed record RequestRequiringTAdmin;

    [RequiresRole("TAdmin", "GestorBU")]
    private sealed record RequestRequiringTAdminOrGestorBU;

    [RequiresRole(AllowAnonymous = true)]
    private sealed record AnonymousRequest;
}
