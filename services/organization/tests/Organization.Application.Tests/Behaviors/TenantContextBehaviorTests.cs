using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Organization.Application.Behaviors;
using Organization.Application.Ports;
using Xunit;

namespace Organization.Application.Tests.Behaviors;

/// <summary>
/// Testes unitários para <see cref="TenantContextBehavior{TRequest,TResponse}"/>.
/// </summary>
public sealed class TenantContextBehaviorTests
{
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IDatabaseContext _databaseContext = Substitute.For<IDatabaseContext>();

    private TenantContextBehavior<FakeRequest, string> CreateBehavior()
        => new(_tenantContext, _databaseContext, NullLogger<TenantContextBehavior<FakeRequest, string>>.Instance);

    [Fact]
    public async Task Handle_WhenTenantIdIsEmpty_ThrowsInvalidOperationException()
    {
        // Arrange
        _tenantContext.TenantId.Returns(Guid.Empty);
        var behavior = CreateBehavior();

        // Act
        var act = () => behavior.Handle(
            new FakeRequest(),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenant_id*");
    }

    [Fact]
    public async Task Handle_WithValidTenantId_CallsSetTenantOnDatabase()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());

        var behavior = CreateBehavior();

        // Act
        var result = await behavior.Handle(
            new FakeRequest(),
            _ => Task.FromResult("resultado"),
            CancellationToken.None);

        // Assert
        result.Should().Be("resultado");
        await _databaseContext.Received(1).SetTenantAsync(tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidTenantId_CallsNextDelegate()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());

        var nextCalled = false;
        var behavior = CreateBehavior();

        // Act
        await behavior.Handle(
            new FakeRequest(),
            _ => { nextCalled = true; return Task.FromResult("ok"); },
            CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SetTenantCalledBeforeNext()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());

        var callOrder = new List<string>();
        _databaseContext.SetTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("SetTenant"); return Task.CompletedTask; });

        var behavior = CreateBehavior();

        // Act
        await behavior.Handle(
            new FakeRequest(),
            _ => { callOrder.Add("Next"); return Task.FromResult("ok"); },
            CancellationToken.None);

        // Assert
        callOrder.Should().ContainInOrder("SetTenant", "Next");
    }

    private sealed record FakeRequest;
}
