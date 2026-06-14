namespace ActivityManagement.Domain.Tests.Activities.Exceptions;

using ActivityManagement.Domain.Activities.Exceptions;

/// <summary>
/// Testes para as exceções de domínio do módulo activity-management.
/// Verifica: herança de base comum, mensagem não vazia, distinção entre tipos.
/// Mapeia: design §4, TASK-03 ST-01.
/// </summary>
public sealed class DomainExceptionsTests
{
    // ── TitleRequiredException ───────────────────────────────────────────────

    [Fact]
    public void TitleRequiredException_IsActivityDomainException()
    {
        var ex = new TitleRequiredException();
        ex.Should().BeAssignableTo<ActivityDomainException>();
    }

    [Fact]
    public void TitleRequiredException_HasNonEmptyMessage()
    {
        var ex = new TitleRequiredException();
        ex.Message.Should().NotBeNullOrWhiteSpace();
    }

    // ── InvalidActivityTypeException ─────────────────────────────────────────

    [Fact]
    public void InvalidActivityTypeException_IsActivityDomainException()
    {
        var ex = new InvalidActivityTypeException("foo");
        ex.Should().BeAssignableTo<ActivityDomainException>();
    }

    [Fact]
    public void InvalidActivityTypeException_HasNonEmptyMessage()
    {
        var ex = new InvalidActivityTypeException("unknown");
        ex.Message.Should().NotBeNullOrWhiteSpace();
        ex.InvalidValue.Should().Be("unknown");
    }

    // ── InvalidStatusTransitionException ────────────────────────────────────

    [Fact]
    public void InvalidStatusTransitionException_IsActivityDomainException()
    {
        var ex = new InvalidStatusTransitionException("completed", "pending");
        ex.Should().BeAssignableTo<ActivityDomainException>();
    }

    [Fact]
    public void InvalidStatusTransitionException_HasNonEmptyMessage()
    {
        var ex = new InvalidStatusTransitionException("completed", "pending");
        ex.Message.Should().NotBeNullOrWhiteSpace();
        ex.FromStatus.Should().Be("completed");
        ex.ToStatus.Should().Be("pending");
    }

    // ── ActivityTerminalException ────────────────────────────────────────────

    [Fact]
    public void ActivityTerminalException_IsActivityDomainException()
    {
        var ex = new ActivityTerminalException("completed");
        ex.Should().BeAssignableTo<ActivityDomainException>();
    }

    [Fact]
    public void ActivityTerminalException_HasNonEmptyMessage()
    {
        var ex = new ActivityTerminalException("cancelled");
        ex.Message.Should().NotBeNullOrWhiteSpace();
        ex.TerminalStatus.Should().Be("cancelled");
    }

    // ── Distinção entre tipos ────────────────────────────────────────────────

    [Fact]
    public void AllExceptionTypes_AreDistinct()
    {
        typeof(TitleRequiredException)
            .Should().NotBe(typeof(InvalidActivityTypeException));

        typeof(InvalidActivityTypeException)
            .Should().NotBe(typeof(InvalidStatusTransitionException));

        typeof(InvalidStatusTransitionException)
            .Should().NotBe(typeof(ActivityTerminalException));
    }
}
