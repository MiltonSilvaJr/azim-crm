using FluentAssertions;
using GoalForecast.Domain.Policies;
using Xunit;

namespace GoalForecast.Domain.Tests.Policies;

/// <summary>
/// Testes de <see cref="BuMembershipSpecification"/>.
/// Cobre TASK-07: IsSatisfied com dados externos de membership.
/// Mapeia: Req 1.4, design §4.6, TASK-07.
/// </summary>
public sealed class BuMembershipSpecificationTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid BuId = Guid.NewGuid();

    [Fact(DisplayName = "BuMembershipSpecification é satisfeita quando isMember=true")]
    public void Specification_WhenMember_IsSatisfied()
    {
        var spec = new BuMembershipSpecification(OwnerId, BuId);

        spec.IsSatisfied(isMember: true).Should().BeTrue();
    }

    [Fact(DisplayName = "BuMembershipSpecification não é satisfeita quando isMember=false")]
    public void Specification_WhenNotMember_IsNotSatisfied()
    {
        var spec = new BuMembershipSpecification(OwnerId, BuId);

        spec.IsSatisfied(isMember: false).Should().BeFalse();
    }

    [Fact(DisplayName = "BuMembershipSpecification expõe OwnerId e BuId corretamente")]
    public void Specification_ExposesIds()
    {
        var spec = new BuMembershipSpecification(OwnerId, BuId);

        spec.OwnerId.Should().Be(OwnerId);
        spec.BuId.Should().Be(BuId);
    }
}
