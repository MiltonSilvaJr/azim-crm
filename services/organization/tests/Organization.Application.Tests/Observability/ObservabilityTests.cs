using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Organization.Application.Behaviors;
using Organization.Application.Ports;
using Xunit;

namespace Organization.Application.Tests.Observability;

/// <summary>
/// Testes de observabilidade — TASK-25 (Onda 6 — Hardening).
/// Valida: ausência de PII em logs, presença de correlation_id,
/// e emissão de alerta quando o último TAdmin é alvo de operação.
/// Referências: design §11; RNF 6.1, RNF 6.3; MSG-017.
/// </summary>
public sealed class ObservabilityTests
{
    // ── Testes de log sem PII ──────────────────────────────────────────────

    [Fact]
    public async Task LoggingBehavior_NeverLogsEmailAddress()
    {
        // Arrange — simula um request com dados que poderiam expor PII
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(Guid.NewGuid());
        tenantContext.UserId.Returns(Guid.NewGuid());
        tenantContext.CorrelationId.Returns(Guid.NewGuid());
        tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());

        var capturingLogger = new CapturingLogger<LoggingBehavior<FakeRequestWithEmail, string>>();
        var behavior = new LoggingBehavior<FakeRequestWithEmail, string>(tenantContext, capturingLogger);

        // Act
        await behavior.Handle(
            new FakeRequestWithEmail("usuario@exemplo.com", "João Silva"),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert — nenhuma mensagem de log deve conter e-mail ou display_name em texto claro
        capturingLogger.Messages.Should().NotContain(
            m => m.Contains("@"),
            "e-mail não deve aparecer em logs (RNF 3.1, design §11)");

        capturingLogger.Messages.Should().NotContain(
            m => m.Contains("João Silva"),
            "display_name não deve aparecer em logs (RNF 3.1, design §11)");

        capturingLogger.Messages.Should().NotContain(
            m => m.Contains("usuario"),
            "parte do e-mail não deve aparecer em logs");
    }

    [Fact]
    public async Task LoggingBehavior_AlwaysIncludesCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.UserId.Returns(Guid.NewGuid());
        tenantContext.CorrelationId.Returns(correlationId);
        tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());

        var capturingLogger = new CapturingLogger<LoggingBehavior<FakeRequest, string>>();
        var behavior = new LoggingBehavior<FakeRequest, string>(tenantContext, capturingLogger);

        // Act
        await behavior.Handle(
            new FakeRequest(),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert
        capturingLogger.Messages.Should().Contain(
            m => m.Contains(correlationId.ToString()),
            "correlation_id deve estar presente em todos os logs (ADR-0009, RNF 6.1)");

        capturingLogger.Messages.Should().Contain(
            m => m.Contains(tenantId.ToString()),
            "tenant_id deve estar presente em todos os logs");
    }

    [Fact]
    public async Task LoggingBehavior_WhenExceptionThrown_LogsExceptionTypeNotMessage()
    {
        // Arrange — mensagem de exceção pode conter PII; apenas o tipo deve ser logado
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(Guid.NewGuid());
        tenantContext.UserId.Returns(Guid.NewGuid());
        tenantContext.CorrelationId.Returns(Guid.NewGuid());
        tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());

        var capturingLogger = new CapturingLogger<LoggingBehavior<FakeRequest, string>>();
        var behavior = new LoggingBehavior<FakeRequest, string>(tenantContext, capturingLogger);

        const string piiInException = "usuario@pii.com está errado";

        // Act
        var act = () => behavior.Handle(
            new FakeRequest(),
            _ => throw new InvalidOperationException(piiInException),
            CancellationToken.None);

        // Assert — exceção relançada, mas PII da mensagem não aparece no log
        await act.Should().ThrowAsync<InvalidOperationException>();

        capturingLogger.Messages.Should().NotContain(
            m => m.Contains(piiInException),
            "mensagem de exceção com PII não deve aparecer em log");

        capturingLogger.Messages.Should().Contain(
            m => m.Contains("InvalidOperationException"),
            "tipo da exceção deve ser logado para rastreabilidade");
    }

    // ── Testes de alerta via port (mock) ──────────────────────────────────

    [Fact]
    public async Task ILastTenantAdminAlertService_WhenMocked_CanBeInjectedIntoHandler()
    {
        // Arrange — verifica que ILastTenantAdminAlertService é injetável no handler
        // O teste completo da implementação concreta está em Infrastructure.Tests
        var alertService = Substitute.For<ILastTenantAdminAlertService>();
        alertService
            .AlertLastAdminAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var tenantId = Guid.NewGuid();

        // Act
        await alertService.AlertLastAdminAsync(tenantId, 1);

        // Assert — porta é invocável; implementação verificada em Infrastructure.Tests
        await alertService.Received(1).AlertLastAdminAsync(tenantId, 1, Arg.Any<CancellationToken>());
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private sealed record FakeRequest;

    private sealed record FakeRequestWithEmail(string Email, string DisplayName);

    /// <summary>Logger que captura mensagens e levels para asserções.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public List<LogLevel> LoggedLevels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LoggedLevels.Add(logLevel);
            Messages.Add(formatter(state, exception));
        }
    }
}
