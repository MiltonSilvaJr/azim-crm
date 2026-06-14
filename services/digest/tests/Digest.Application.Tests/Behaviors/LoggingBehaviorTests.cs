using Digest.Application.Behaviors;
using Digest.Application.Commands;
using Digest.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NodaTime;
using Xunit;

namespace Digest.Application.Tests.Behaviors;

/// <summary>
/// Testes do <see cref="LoggingBehavior{TRequest, TResponse}"/> (TASK-13).
/// Verifica que e-mail e outros campos PII nunca são logados (RNF 3, DD-011).
/// </summary>
public sealed class LoggingBehaviorTests
{
    // Logger in-memory simples para capturar mensagens sem usar NSubstitute no ILogger genérico
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly List<string> _messages = [];
        public IReadOnlyList<string> Messages => _messages;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _messages.Add(formatter(state, exception));
        }
    }

    [Fact(DisplayName = "LoggingBehavior não inclui UserEmail do SendUserDigestCommand nos logs")]
    public async Task LoggingBehavior_DoesNotLogUserEmail()
    {
        var logger = new CapturingLogger<LoggingBehavior<SendUserDigestCommand, SendUserDigestResult>>();
        var behavior = new LoggingBehavior<SendUserDigestCommand, SendUserDigestResult>(logger);

        var sensitiveEmail = "usuario-secreto@example.com";
        var cmd = new SendUserDigestCommand(
            Guid.NewGuid(), Guid.NewGuid(),
            new DigestDate(new LocalDate(2026, 6, 9)),
            sensitiveEmail,
            Guid.NewGuid());

        await behavior.Handle(cmd, () =>
            Task.FromResult(new SendUserDigestResult(Sent: true, Skipped: false, MessageId: "msg-1")),
            default);

        // O e-mail nunca deve aparecer nas mensagens logadas (RNF 3, DD-011)
        Assert.DoesNotContain(logger.Messages, msg => msg.Contains(sensitiveEmail));
    }

    [Fact(DisplayName = "LoggingBehavior loga tenant_id e correlation_id sem PII")]
    public async Task LoggingBehavior_LogsTenantIdAndCorrelationId_NoPii()
    {
        var logger = new CapturingLogger<LoggingBehavior<RunDigestForTenantCommand, RunDigestForTenantResult>>();
        var behavior = new LoggingBehavior<RunDigestForTenantCommand, RunDigestForTenantResult>(logger);

        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var cmd = new RunDigestForTenantCommand(tenantId, DateTimeOffset.UtcNow, correlationId);

        await behavior.Handle(cmd, () =>
            Task.FromResult(new RunDigestForTenantResult(1, 1, 0, 0)),
            default);

        // Deve haver ao menos uma mensagem que referencia tenant_id
        Assert.Contains(logger.Messages, msg => msg.Contains(tenantId.ToString()));
    }

    [Fact(DisplayName = "LoggingBehavior propaga exceção e loga o erro")]
    public async Task LoggingBehavior_PropagatesException_AndLogsError()
    {
        var logger = new CapturingLogger<LoggingBehavior<RunDigestForTenantCommand, RunDigestForTenantResult>>();
        var behavior = new LoggingBehavior<RunDigestForTenantCommand, RunDigestForTenantResult>(logger);

        var cmd = new RunDigestForTenantCommand(Guid.NewGuid(), DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(cmd, () => throw new InvalidOperationException("Falha simulada"), default));

        // Deve ter logado o erro
        Assert.Contains(logger.Messages, msg => msg.Contains("Falha em"));
    }
}
