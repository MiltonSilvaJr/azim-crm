namespace ActivityManagement.Domain.Tests.Activities.ValueObjects;

using ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Testes unitários para os objetos de valor <see cref="OpportunityLink"/> e <see cref="AccountLink"/>.
/// Mapeia: design §4.3, Req 3.1, TASK-02.
/// </summary>
public sealed class OpportunityLinkTests
{
    [Fact]
    public void Create_ValidGuid_Succeeds()
    {
        var id = Guid.NewGuid();
        var link = OpportunityLink.Create(id);
        link.OpportunityId.Should().Be(id);
    }

    [Fact]
    public void Create_EmptyGuid_ThrowsArgumentException()
    {
        var act = () => OpportunityLink.Create(Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TwoInstances_SameValue_AreEqual()
    {
        var id = Guid.NewGuid();
        var a = OpportunityLink.Create(id);
        var b = OpportunityLink.Create(id);
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void TwoInstances_DifferentValue_AreNotEqual()
    {
        var a = OpportunityLink.Create(Guid.NewGuid());
        var b = OpportunityLink.Create(Guid.NewGuid());
        a.Should().NotBe(b);
    }
}

public sealed class AccountLinkTests
{
    [Fact]
    public void Create_ValidGuid_Succeeds()
    {
        var id = Guid.NewGuid();
        var link = AccountLink.Create(id);
        link.AccountId.Should().Be(id);
    }

    [Fact]
    public void Create_EmptyGuid_ThrowsArgumentException()
    {
        var act = () => AccountLink.Create(Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TwoInstances_SameValue_AreEqual()
    {
        var id = Guid.NewGuid();
        var a = AccountLink.Create(id);
        var b = AccountLink.Create(id);
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }
}
