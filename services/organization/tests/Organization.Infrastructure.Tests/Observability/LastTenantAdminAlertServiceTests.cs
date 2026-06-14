using FluentAssertions;
using Microsoft.Extensions.Logging;
using Organization.Infrastructure.Observability;
using Xunit;

namespace Organization.Infrastructure.Tests.Observability;

/// <summary>
/// Testes do serviço de alerta de último TAdmin — TASK-25 ST-05/ST-06 (Onda 6 — Hardening).
/// Valida: emissão de Warning, ausência de PII, presença do TenantId.
/// Referência: design §11; RNF 6.3; MSG-017; RISK-ORG-01.
/// </summary>
public sealed class LastTenantAdminAlertServiceTests
{
    [Fact]
    public async Task AlertLastAdminAsync_EmitsWarningLevel()
    {
        // Arrange — ST-05: alerta emitido quando contador retorna 1
        var logger = new CapturingLogger<LastTenantAdminAlertService>();
        var sut = new LastTenantAdminAlertService(logger);

        // Act
        await sut.AlertLastAdminAsync(tenantId: Guid.NewGuid(), remainingAdminCount: 1);

        // Assert — deve ser Warning para disparo de alerta operacional (RNF 6.3)
        logger.LoggedLevels.Should().Contain(LogLevel.Warning,
            "alerta de último TAdmin deve ter nível Warning (RNF 6.3, MSG-017)");
    }

    [Fact]
    public async Task AlertLastAdminAsync_IncludesTenantIdInLog()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var logger = new CapturingLogger<LastTenantAdminAlertService>();
        var sut = new LastTenantAdminAlertService(logger);

        // Act
        await sut.AlertLastAdminAsync(tenantId, remainingAdminCount: 1);

        // Assert
        logger.Messages.Should().Contain(
            m => m.Contains(tenantId.ToString()),
            "o tenant afetado deve ser identificado no alerta para rastreabilidade");
    }

    [Fact]
    public async Task AlertLastAdminAsync_NeverContainsPii()
    {
        // Arrange
        var logger = new CapturingLogger<LastTenantAdminAlertService>();
        var sut = new LastTenantAdminAlertService(logger);

        // Act
        await sut.AlertLastAdminAsync(Guid.NewGuid(), remainingAdminCount: 1);

        // Assert — sem PII (design §11, RNF 3.1)
        logger.Messages.Should().NotContain(
            m => m.Contains("@"),
            "e-mail não deve aparecer no alerta");

        logger.Messages.Should().NotContain(
            m => m.Contains("display_name") || m.Contains("email"),
            "campos de PII não devem aparecer no alerta");
    }

    [Fact]
    public async Task AlertLastAdminAsync_IncludesRemainingCountInMessage()
    {
        // Arrange
        var logger = new CapturingLogger<LastTenantAdminAlertService>();
        var sut = new LastTenantAdminAlertService(logger);

        // Act
        await sut.AlertLastAdminAsync(Guid.NewGuid(), remainingAdminCount: 1);

        // Assert — contagem para diagnóstico operacional
        logger.Messages.Should().Contain(
            m => m.Contains("1"),
            "contagem de TAdmins restantes deve aparecer no alerta para diagnóstico");
    }

    [Fact]
    public async Task AlertLastAdminAsync_CompletesSuccessfully_WithoutThrowing()
    {
        // Arrange
        var logger = new CapturingLogger<LastTenantAdminAlertService>();
        var sut = new LastTenantAdminAlertService(logger);

        // Act & Assert — alerta nunca deve lançar exceção
        var act = () => sut.AlertLastAdminAsync(Guid.NewGuid(), 1);
        await act.Should().NotThrowAsync();
    }

    // ── Infra de suporte ───────────────────────────────────────────────────

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
