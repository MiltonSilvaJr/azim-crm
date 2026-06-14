namespace ActivityManagement.Domain.Tests.Activities.ValueObjects;

using ActivityManagement.Domain.Activities.ValueObjects;
using ActivityManagement.Domain.Activities.Exceptions;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="ActivityStatus"/> e sua máquina de estados.
/// Mapeia: design §4.3, §4.5, Req 4, TASK-02.
/// </summary>
public sealed class ActivityStatusTests
{
    // ── Valores canônicos ────────────────────────────────────────────────────

    [Theory]
    [InlineData("pending")]
    [InlineData("in_progress")]
    [InlineData("completed")]
    [InlineData("cancelled")]
    public void Create_ValidValue_Succeeds(string value)
    {
        var status = ActivityStatus.Create(value);
        status.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("PENDING")]
    [InlineData("done")]
    [InlineData("overdue")]
    public void Create_InvalidValue_ThrowsArgumentException(string value)
    {
        var act = () => ActivityStatus.Create(value);
        act.Should().Throw<ArgumentException>();
    }

    // ── Terminais ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("completed")]
    [InlineData("cancelled")]
    public void IsTerminal_TerminalStatuses_ReturnsTrue(string value)
    {
        var status = ActivityStatus.Create(value);
        status.IsTerminal.Should().BeTrue();
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("in_progress")]
    public void IsTerminal_NonTerminalStatuses_ReturnsFalse(string value)
    {
        var status = ActivityStatus.Create(value);
        status.IsTerminal.Should().BeFalse();
    }

    // ── Transições válidas ───────────────────────────────────────────────────

    [Theory]
    [InlineData("pending",     "in_progress")]
    [InlineData("pending",     "completed")]
    [InlineData("pending",     "cancelled")]
    [InlineData("in_progress", "pending")]
    [InlineData("in_progress", "completed")]
    [InlineData("in_progress", "cancelled")]
    public void CanTransitionTo_ValidTransitions_ReturnsTrue(string from, string to)
    {
        var status = ActivityStatus.Create(from);
        var target = ActivityStatus.Create(to);
        status.CanTransitionTo(target).Should().BeTrue();
    }

    // ── Transições inválidas ─────────────────────────────────────────────────

    [Theory]
    [InlineData("completed",   "pending")]
    [InlineData("completed",   "in_progress")]
    [InlineData("completed",   "cancelled")]
    [InlineData("cancelled",   "pending")]
    [InlineData("cancelled",   "in_progress")]
    [InlineData("cancelled",   "completed")]
    [InlineData("pending",     "pending")]
    [InlineData("in_progress", "in_progress")]
    public void CanTransitionTo_InvalidTransitions_ReturnsFalse(string from, string to)
    {
        var status = ActivityStatus.Create(from);
        var target = ActivityStatus.Create(to);
        status.CanTransitionTo(target).Should().BeFalse();
    }

    // ── Igualdade ────────────────────────────────────────────────────────────

    [Fact]
    public void TwoInstances_SameValue_AreEqual()
    {
        var a = ActivityStatus.Create("pending");
        var b = ActivityStatus.Create("pending");
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Defaults_ArePending()
    {
        ActivityStatus.Pending.Value.Should().Be("pending");
    }
}
