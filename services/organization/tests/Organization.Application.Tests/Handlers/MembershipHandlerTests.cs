using FluentAssertions;
using NSubstitute;
using Organization.Application.Commands.Membership;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Application.Tests.Handlers;

/// <summary>
/// Testes unitários para os handlers de membership.
/// </summary>
public sealed class MembershipHandlerTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly ITenantAdminCounter _adminCounter = Substitute.For<ITenantAdminCounter>();
    private readonly IMembershipCache _cache = Substitute.For<IMembershipCache>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IEventOutbox _outbox = Substitute.For<IEventOutbox>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public MembershipHandlerTests()
    {
        _tenantContext.TenantId.Returns(_tenantId);
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());
    }

    // ── AssignMembershipCommandHandler ────────────────────────────────────────

    [Fact]
    public async Task AssignMembership_NewMembership_CreatesAndInvalidatesCache()
    {
        // Arrange
        var user = CreateActiveUser();
        var buId = Guid.NewGuid();
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new AssignMembershipCommandHandler(_userRepo, _cache, _tenantContext);

        // Act
        await handler.Handle(new AssignMembershipCommand(user.Id, buId, "Vendedor"), CancellationToken.None);

        // Assert
        user.Memberships.Should().ContainSingle(m => m.BuId == buId && m.Role == Role.Vendedor);
        await _cache.Received(1).InvalidateAsync(_tenantId, user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignMembership_DuplicateBu_ThrowsDomainException()
    {
        // Arrange
        var user = CreateActiveUser();
        var buId = Guid.NewGuid();
        user.AssignMembership(buId, Role.Vendedor, Guid.NewGuid());
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new AssignMembershipCommandHandler(_userRepo, _cache, _tenantContext);

        // Act
        var act = () => handler.Handle(new AssignMembershipCommand(user.Id, buId, "GestorBU"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Domain.Exceptions.DomainException>()
            .WithMessage("*ORG-ERR-012*");
    }

    [Fact]
    public async Task AssignMembership_InvalidRole_ThrowsArgumentException()
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new AssignMembershipCommandHandler(_userRepo, _cache, _tenantContext);

        // Act
        var act = () => handler.Handle(new AssignMembershipCommand(user.Id, Guid.NewGuid(), "PlatOp"), CancellationToken.None);

        // Assert — Role.Create deve rejeitar PlatOp (papel de plataforma, não é membership)
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*PlatOp*não é válido*");
    }

    // ── ChangeMembershipRoleCommandHandler ────────────────────────────────────

    [Fact]
    public async Task ChangeMembershipRole_DowngradingLastTAdmin_ThrowsORG_ERR_009()
    {
        // Arrange — usuário é TAdmin e é o único TAdmin do tenant
        var user = CreateActiveUser();
        var buId = Guid.NewGuid();
        user.AssignMembership(buId, Role.TAdmin, Guid.NewGuid());
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _adminCounter.CountActiveTenantAdminsAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(1);

        var handler = new ChangeMembershipRoleCommandHandler(_userRepo, _adminCounter, _cache, _tenantContext, _outbox);

        // Act
        var act = () => handler.Handle(
            new ChangeMembershipRoleCommand(user.Id, buId, "GestorBU"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ORG-ERR-009*");
    }

    [Fact]
    public async Task ChangeMembershipRole_DowngradingTAdminWithOthers_Succeeds()
    {
        // Arrange — há 2 TAdmins; pode rebaixar
        var user = CreateActiveUser();
        var buId = Guid.NewGuid();
        user.AssignMembership(buId, Role.TAdmin, Guid.NewGuid());
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _adminCounter.CountActiveTenantAdminsAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(2);

        var handler = new ChangeMembershipRoleCommandHandler(_userRepo, _adminCounter, _cache, _tenantContext, _outbox);

        // Act
        await handler.Handle(
            new ChangeMembershipRoleCommand(user.Id, buId, "GestorBU"),
            CancellationToken.None);

        // Assert
        user.Memberships.Should().ContainSingle(m => m.BuId == buId && m.Role == Role.GestorBU);
        await _cache.Received(1).InvalidateAsync(_tenantId, user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeMembershipRole_PromotingToTAdmin_DoesNotCheckPolicy()
    {
        // Arrange — promoção para TAdmin nunca precisa checar a policy
        var user = CreateActiveUser();
        var buId = Guid.NewGuid();
        user.AssignMembership(buId, Role.Vendedor, Guid.NewGuid());
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new ChangeMembershipRoleCommandHandler(_userRepo, _adminCounter, _cache, _tenantContext, _outbox);

        // Act
        await handler.Handle(
            new ChangeMembershipRoleCommand(user.Id, buId, "TAdmin"),
            CancellationToken.None);

        // Assert — counter não deve ser consultado em promoção
        await _adminCounter.DidNotReceive().CountActiveTenantAdminsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── RemoveMembershipCommandHandler ────────────────────────────────────────

    [Fact]
    public async Task RemoveMembership_LastTAdmin_ThrowsORG_ERR_009()
    {
        // Arrange
        var user = CreateActiveUser();
        var buId = Guid.NewGuid();
        user.AssignMembership(buId, Role.TAdmin, Guid.NewGuid());
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _adminCounter.CountActiveTenantAdminsAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(1);

        var handler = new RemoveMembershipCommandHandler(_userRepo, _adminCounter, _cache, _tenantContext);

        // Act
        var act = () => handler.Handle(new RemoveMembershipCommand(user.Id, buId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ORG-ERR-009*");
    }

    [Fact]
    public async Task RemoveMembership_NonTAdmin_DoesNotCheckPolicy()
    {
        // Arrange
        var user = CreateActiveUser();
        var buId = Guid.NewGuid();
        user.AssignMembership(buId, Role.Vendedor, Guid.NewGuid());
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new RemoveMembershipCommandHandler(_userRepo, _adminCounter, _cache, _tenantContext);

        // Act
        await handler.Handle(new RemoveMembershipCommand(user.Id, buId), CancellationToken.None);

        // Assert — Vendedor não é TAdmin; counter não deve ser consultado
        await _adminCounter.DidNotReceive().CountActiveTenantAdminsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        user.Memberships.Should().BeEmpty();
        await _cache.Received(1).InvalidateAsync(_tenantId, user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveMembership_TAdminWithOthers_RemovesSuccessfully()
    {
        // Arrange — há 3 TAdmins; pode remover
        var user = CreateActiveUser();
        var buId = Guid.NewGuid();
        user.AssignMembership(buId, Role.TAdmin, Guid.NewGuid());
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _adminCounter.CountActiveTenantAdminsAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(3);

        var handler = new RemoveMembershipCommandHandler(_userRepo, _adminCounter, _cache, _tenantContext);

        // Act
        await handler.Handle(new RemoveMembershipCommand(user.Id, buId), CancellationToken.None);

        // Assert
        user.Memberships.Should().BeEmpty();
    }

    // ── Helpers ──

    private User CreateActiveUser()
        => User.Activate("user@test.com", "Usuário Teste", "uid-test", _tenantId, _now);
}
