using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Organization.Application.Behaviors;
using Organization.Application.Ports;
using Xunit;

namespace Organization.Application.Tests.Behaviors;

/// <summary>
/// Testes unitários para <see cref="LoggingBehavior{TRequest,TResponse}"/>.
/// </summary>
public sealed class LoggingBehaviorTests
{
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private LoggingBehavior<FakeRequest, string> CreateBehavior(ILogger<LoggingBehavior<FakeRequest, string>>? logger = null)
        => new(_tenantContext, logger ?? new CapturingLogger<LoggingBehavior<FakeRequest, string>>());

    [Fact]
    public async Task Handle_CallsNextDelegate()
    {
        // Arrange
        SetupTenantContext();
        var behavior = CreateBehavior();
        var nextCalled = false;

        // Act
        await behavior.Handle(
            new FakeRequest(),
            _ => { nextCalled = true; return Task.FromResult("ok"); },
            CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsResultFromNext()
    {
        // Arrange
        SetupTenantContext();
        var behavior = CreateBehavior();

        // Act
        var result = await behavior.Handle(
            new FakeRequest(),
            _ => Task.FromResult("resultado"),
            CancellationToken.None);

        // Assert
        result.Should().Be("resultado");
    }

    [Fact]
    public async Task Handle_WhenNextThrows_RethrowsException()
    {
        // Arrange
        SetupTenantContext();
        var behavior = CreateBehavior();

        // Act
        var act = () => behavior.Handle(
            new FakeRequest(),
            _ => throw new InvalidOperationException("erro"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_LogsWithoutPii()
    {
        // Arrange — verifica que o log não contém e-mail nem display_name
        SetupTenantContext();
        var capturingLogger = new CapturingLogger<LoggingBehavior<FakeRequest, string>>();
        var behavior = CreateBehavior(capturingLogger);

        // Act
        await behavior.Handle(
            new FakeRequest(),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert — nenhuma mensagem de log deve conter PII
        capturingLogger.Messages.Should().NotContain(m => m.Contains("@"), "e-mail não deve aparecer em logs");
        capturingLogger.Messages.Should().NotContain(m => m.Contains("display_name"), "display_name não deve aparecer em logs");
    }

    [Fact]
    public async Task Handle_LogsCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        SetupTenantContext(correlationId: correlationId);
        var capturingLogger = new CapturingLogger<LoggingBehavior<FakeRequest, string>>();
        var behavior = CreateBehavior(capturingLogger);

        // Act
        await behavior.Handle(
            new FakeRequest(),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert
        capturingLogger.Messages.Should().Contain(m => m.Contains(correlationId.ToString()));
    }

    private void SetupTenantContext(Guid? correlationId = null)
    {
        _tenantContext.TenantId.Returns(Guid.NewGuid());
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(correlationId ?? Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());
    }

    private sealed record FakeRequest;

    /// <summary>Logger que captura mensagens formatadas para asserções.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
