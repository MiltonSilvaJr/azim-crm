namespace ActivityManagement.Domain.Tests.Activities.ValueObjects;

using ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="Priority"/>.
/// Mapeia: design §4.3, Req 1.3, TASK-02.
/// </summary>
public sealed class PriorityTests
{
    [Theory]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    public void Create_ValidValue_Succeeds(string value)
    {
        var priority = Priority.Create(value);
        priority.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("LOW")]
    [InlineData("critical")]
    [InlineData("MEDIUM")]
    public void Create_InvalidValue_ThrowsArgumentException(string value)
    {
        var act = () => Priority.Create(value);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Default_IsMedium()
    {
        Priority.Default.Value.Should().Be("medium");
    }

    [Fact]
    public void TwoInstances_SameValue_AreEqual()
    {
        var a = Priority.Create("high");
        var b = Priority.Create("high");
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void TwoInstances_DifferentValue_AreNotEqual()
    {
        var a = Priority.Create("low");
        var b = Priority.Create("high");
        a.Should().NotBe(b);
    }
}
