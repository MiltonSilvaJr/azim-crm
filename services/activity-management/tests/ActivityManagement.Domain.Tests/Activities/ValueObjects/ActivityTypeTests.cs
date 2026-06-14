namespace ActivityManagement.Domain.Tests.Activities.ValueObjects;

using ActivityManagement.Domain.Activities.ValueObjects;
using ActivityManagement.Domain.Activities.Exceptions;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="ActivityType"/>.
/// Mapeia: design §4.3, Req 1.6, TASK-02.
/// </summary>
public sealed class ActivityTypeTests
{
    [Theory]
    [InlineData("meeting")]
    [InlineData("follow_up")]
    [InlineData("call")]
    [InlineData("email")]
    [InlineData("task")]
    public void Create_ValidValue_Succeeds(string value)
    {
        var type = ActivityType.Create(value);
        type.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("MEETING")]
    [InlineData("followup")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("unknown")]
    [InlineData("meeting ")]
    public void Create_InvalidValue_ThrowsInvalidActivityTypeException(string value)
    {
        var act = () => ActivityType.Create(value);
        act.Should().Throw<InvalidActivityTypeException>();
    }

    [Fact]
    public void TwoInstances_SameValue_AreEqual()
    {
        var a = ActivityType.Create("meeting");
        var b = ActivityType.Create("meeting");
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void TwoInstances_DifferentValue_AreNotEqual()
    {
        var a = ActivityType.Create("meeting");
        var b = ActivityType.Create("call");
        a.Should().NotBe(b);
    }

    [Fact]
    public void GetHashCode_SameValue_Same()
    {
        var a = ActivityType.Create("email");
        var b = ActivityType.Create("email");
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void AllCanonical_AreFive()
    {
        ActivityType.AllValues.Should().HaveCount(5);
    }
}
