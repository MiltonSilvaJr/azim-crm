using FluentAssertions;
using NSubstitute;
using Organization.Application.Commands.User;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Application.Tests.Handlers;

/// <summary>
/// Testes unitários para o handler de desativação de usuário.
/// </summary>
public sealed class DeactivateUserHandlerTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly ITenantAdminCounter _adminCounter = Substitute.For<ITenantAdminCounter>();
    private readonly IActivityCounter _activityCounter = Substitute.For<IActivityCounter>();
    private readonly IMembershipCache _cache = Substitute.For<IMembershipCache>();
    private readonly IEventOutbox _outbox = Substitute.For<IEventOutbox>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IOrganizationMetrics _metrics = Substitute.For<IOrganizationMetrics>();
    private readonly ILastTenantAdminAlertService _lastAdminAlert = Substitute.For<ILastTenantAdminAlertService>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public DeactivateUserHandlerTests()
    {
        _tenantContext.TenantId.Returns(_tenantId);
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());
        _clock.UtcNow.Returns(_now);
    }

    [Fact]
    public async Task DeactivateUser_NonTAdmin_DeactivatesAndInvalidatesCache()
    {
        // Arrange
        var user = CreateActiveUser();
        var buId = Guid.NewGuid();
        user.AssignMembership(buId, Role.Vendedor, Guid.NewGuid());
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _activityCounter.CountFutureAsync(_tenantId, user.Id, Arg.Any<CancellationToken>()).Returns(0);

        var handler = CreateHandler();

        // Act
        await handler.Handle(new DeactivateUserCommand(user.Id), CancellationToken.None);

        // Assert
        user.Active.Should().BeFalse();
        await _cache.Received(1).InvalidateAsync(_tenantId, user.Id, Arg.Any<CancellationToken>());
        await _userRepo.Received(1).SaveAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateUser_LastTAdmin_ThrowsORG_ERR_009()
    {
        // Arrange
        var user = CreateActiveUser();
        var buId = Guid.NewGuid();
        user.AssignMembership(buId, Role.TAdmin, Guid.NewGuid());
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _adminCounter.CountActiveTenantAdminsAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(1);

        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(new DeactivateUserCommand(user.Id), CancellationToken.None);

        // Assert — desativação do último TAdmin é proibida
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ORG-ERR-009*");
        user.Active.Should().BeTrue(); // estado não foi alterado
    }

    [Fact]
    public async Task DeactivateUser_WithFutureActivities_ThrowsORG_ERR_011()
    {
        // Arrange
        var user = CreateActiveUser();
        var buId = Guid.NewGuid();
        user.AssignMembership(buId, Role.Vendedor, Guid.NewGuid());
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _activityCounter.CountFutureAsync(_tenantId, user.Id, Arg.Any<CancellationToken>()).Returns(3);

        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(new DeactivateUserCommand(user.Id), CancellationToken.None);

        // Assert — usuário com atividades futuras não pode ser desativado
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ORG-ERR-011*");
        user.Active.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateUser_TAdminWithOthers_Succeeds()
    {
        // Arrange — há 2 TAdmins; pode desativar
        var user = CreateActiveUser();
        var buId = Guid.NewGuid();
        user.AssignMembership(buId, Role.TAdmin, Guid.NewGuid());
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _adminCounter.CountActiveTenantAdminsAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(2);
        _activityCounter.CountFutureAsync(_tenantId, user.Id, Arg.Any<CancellationToken>()).Returns(0);

        var handler = CreateHandler();

        // Act
        await handler.Handle(new DeactivateUserCommand(user.Id), CancellationToken.None);

        // Assert
        user.Active.Should().BeFalse();
        await _cache.Received(1).InvalidateAsync(_tenantId, user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateUser_Succeeds_EmitsUserDeactivatedViaOutbox()
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _activityCounter.CountFutureAsync(_tenantId, user.Id, Arg.Any<CancellationToken>()).Returns(0);

        var handler = CreateHandler();

        // Act
        await handler.Handle(new DeactivateUserCommand(user.Id), CancellationToken.None);

        // Assert — evento emitido via Outbox
        await _outbox.Received(1).EnqueueAsync(
            Arg.Any<Organization.Domain.Events.UserDeactivated>(),
            _tenantId,
            _tenantContext.CorrelationId,
            Arg.Any<CancellationToken>());
    }

    // ── Helpers ──

    private User CreateActiveUser()
        => User.Activate("user@test.com", "Usuário Teste", "uid-test", _tenantId, _now);

    private DeactivateUserCommandHandler CreateHandler()
        => new(_userRepo, _adminCounter, _activityCounter, _cache, _outbox, _clock, _tenantContext, _metrics, _lastAdminAlert);
}
