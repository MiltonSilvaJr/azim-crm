namespace ActivityManagement.Domain.Tests.Activities.ValueObjects;

using ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="DueDate"/>.
/// Mapeia: design §4.3, Req 1.1, TASK-02.
/// </summary>
public sealed class DueDateTests
{
    [Fact]
    public void Create_ValidDateTimeOffset_Succeeds()
    {
        var instant = DateTimeOffset.UtcNow.AddDays(1);
        var dueDate = DueDate.Create(instant);
        dueDate.Value.Should().Be(instant);
    }

    [Fact]
    public void TwoInstances_SameValue_AreEqual()
    {
        var instant = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);
        var a = DueDate.Create(instant);
        var b = DueDate.Create(instant);
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void TwoInstances_DifferentValue_AreNotEqual()
    {
        var a = DueDate.Create(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));
        var b = DueDate.Create(new DateTimeOffset(2026, 6, 16, 10, 0, 0, TimeSpan.Zero));
        a.Should().NotBe(b);
    }

    [Fact]
    public void GetHashCode_SameValue_Same()
    {
        var instant = DateTimeOffset.UtcNow;
        var a = DueDate.Create(instant);
        var b = DueDate.Create(instant);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
