using FluentAssertions;
using Organization.Domain.Aggregates;
using Organization.Domain.Events;
using Organization.Domain.Exceptions;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Domain.Tests.Aggregates;

/// <summary>
/// Testes unitários para o agregado <see cref="User"/> com <see cref="UserMembership"/>.
/// Cobre: ativação, soft-delete, MembershipUniquenessSpec, domain events, histórico preservado.
/// </summary>
public sealed class UserTests
{
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BuId1 = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid BuId2 = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // ── Activate ─────────────────────────────────────────────────────────────

    [Fact]
    public void Activate_WhenValidArgs_ShouldBeActive()
    {
        var user = User.Activate("user@example.com", "João Silva", "uid_123", TenantId, DateTimeOffset.UtcNow);
        user.Active.Should().BeTrue();
    }

    [Fact]
    public void Activate_ShouldRaiseUserActivatedEvent()
    {
        var memberships = new[] { (BuId1, Role.TAdmin) };
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        user.AssignMembership(BuId1, Role.TAdmin, Guid.NewGuid());
        user.ClearDomainEvents();

        // O evento UserActivated é levantado no Activate
        var user2 = User.Activate("user2@example.com", "Ana", "uid_456", TenantId, DateTimeOffset.UtcNow);
        user2.DomainEvents.Should().ContainSingle(e => e is UserActivated);
    }

    [Fact]
    public void Activate_WhenEmailIsNull_ShouldThrow()
    {
        var act = () => User.Activate(null!, "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Activate_WhenIdentityUidIsNull_ShouldThrow()
    {
        var act = () => User.Activate("user@example.com", "João", null!, TenantId, DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Activate_ShouldSetTenantId()
    {
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        user.TenantId.Should().Be(TenantId);
    }

    // ── AssignMembership ─────────────────────────────────────────────────────

    [Fact]
    public void AssignMembership_WhenNewBu_ShouldBeInMemberships()
    {
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        user.AssignMembership(BuId1, Role.TAdmin, Guid.NewGuid());
        user.Memberships.Should().ContainSingle(m => m.BuId == BuId1 && m.Role == Role.TAdmin);
    }

    [Fact]
    public void AssignMembership_WhenSameBuTwice_ShouldThrow()
    {
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        user.AssignMembership(BuId1, Role.TAdmin, Guid.NewGuid());
        var act = () => user.AssignMembership(BuId1, Role.GestorBU, Guid.NewGuid());
        act.Should().Throw<DomainException>().WithMessage("*ORG-ERR-012*");
    }

    [Fact]
    public void AssignMembership_WhenDifferentBus_ShouldSucceed()
    {
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        user.AssignMembership(BuId1, Role.TAdmin, Guid.NewGuid());
        user.AssignMembership(BuId2, Role.Vendedor, Guid.NewGuid());
        user.Memberships.Should().HaveCount(2);
    }

    // ── ChangeMembershipRole ──────────────────────────────────────────────────

    [Fact]
    public void ChangeMembershipRole_WhenValid_ShouldUpdateRole()
    {
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        user.AssignMembership(BuId1, Role.Vendedor, Guid.NewGuid());
        user.ClearDomainEvents();
        user.ChangeMembershipRole(BuId1, Role.GestorBU);
        user.Memberships.First(m => m.BuId == BuId1).Role.Should().Be(Role.GestorBU);
    }

    [Fact]
    public void ChangeMembershipRole_ShouldRaiseMembershipRoleChangedEvent()
    {
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        user.AssignMembership(BuId1, Role.Vendedor, Guid.NewGuid());
        user.ClearDomainEvents();
        user.ChangeMembershipRole(BuId1, Role.GestorBU);
        user.DomainEvents.Should().ContainSingle(e => e is MembershipRoleChanged);
    }

    [Fact]
    public void ChangeMembershipRole_WhenBuNotFound_ShouldThrow()
    {
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        var act = () => user.ChangeMembershipRole(BuId1, Role.TAdmin);
        act.Should().Throw<DomainException>();
    }

    // ── RemoveMembership ──────────────────────────────────────────────────────

    [Fact]
    public void RemoveMembership_WhenExists_ShouldRemoveFromCollection()
    {
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        user.AssignMembership(BuId1, Role.TAdmin, Guid.NewGuid());
        user.RemoveMembership(BuId1);
        user.Memberships.Should().BeEmpty();
    }

    [Fact]
    public void RemoveMembership_WhenBuNotFound_ShouldThrow()
    {
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        var act = () => user.RemoveMembership(BuId1);
        act.Should().Throw<DomainException>();
    }

    // ── Deactivate ───────────────────────────────────────────────────────────

    [Fact]
    public void Deactivate_ShouldSetActiveToFalse()
    {
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        user.Deactivate(DateTimeOffset.UtcNow);
        user.Active.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_ShouldFillDeactivatedAt()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        user.Deactivate(now);
        user.DeactivatedAt.Should().Be(now);
    }

    [Fact]
    public void Deactivate_ShouldRaiseUserDeactivatedEvent()
    {
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        user.ClearDomainEvents();
        user.Deactivate(DateTimeOffset.UtcNow);
        user.DomainEvents.Should().ContainSingle(e => e is UserDeactivated);
    }

    [Fact]
    public void Deactivate_ShouldPreserveMemberships()
    {
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        user.AssignMembership(BuId1, Role.TAdmin, Guid.NewGuid());
        user.AssignMembership(BuId2, Role.Vendedor, Guid.NewGuid());
        user.Deactivate(DateTimeOffset.UtcNow);

        // Histórico de memberships deve ser preservado (sem deleção física)
        user.Memberships.Should().HaveCount(2);
    }

    [Fact]
    public void Deactivate_WhenAlreadyDeactivated_ShouldThrow()
    {
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        user.Deactivate(DateTimeOffset.UtcNow);
        var act = () => user.Deactivate(DateTimeOffset.UtcNow);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AssignMembership_WhenDeactivated_ShouldThrow()
    {
        var user = User.Activate("user@example.com", "João", "uid_123", TenantId, DateTimeOffset.UtcNow);
        user.Deactivate(DateTimeOffset.UtcNow);
        var act = () => user.AssignMembership(BuId1, Role.TAdmin, Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }
}
