using Authentication.Infrastructure.Logging;
using FluentAssertions;
using Serilog.Core;
using Serilog.Events;

namespace Authentication.Infrastructure.Tests.Logging;

/// <summary>
/// Testes de scan de saída de log — verifica que PII não vaza em texto claro.
///
/// Verifica que:
///   - E-mail em texto claro é mascarado pelo destructuring policy
///   - identity_uid nunca aparece em logs (confinado ao adapter — Architecture.Tests garantem)
///   - Campos não-PII (user_id, tenant_id, correlation_id) passam sem alteração
///
/// Mapeia: TASK-22, RNF 4, design.md § 11, DD-006.
/// </summary>
public sealed class SerilogPiiMaskingTests
{
    // Sink de captura de eventos para inspeção
    private sealed class CaptureSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    [Fact]
    public void MaskEmailPolicy_WhenEmailPropertyPresent_MasksIt()
    {
        // Arrange
        var sink = new CaptureSink();
        var logger = new Serilog.LoggerConfiguration()
            .Destructure.With<MaskEmailDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        // Act
        logger.Information("User logged in {@User}", new { Email = "alice@acme.com", Role = "viewer" });

        // Assert — e-mail não aparece em texto claro na representação do log
        var text = sink.Events[0].RenderMessage();
        text.Should().NotContain("alice@acme.com");
        text.Should().NotContain("@acme.com");
        // O campo Role não deve ser mascarado
        text.Should().Contain("viewer");
    }

    [Fact]
    public void MaskEmailPolicy_WhenNamePropertyPresent_MasksIt()
    {
        // Arrange
        var sink = new CaptureSink();
        var logger = new Serilog.LoggerConfiguration()
            .Destructure.With<MaskEmailDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        // Act — nome não deve aparecer em logs
        logger.Information("Processing request {@Context}", new { Name = "Alice Smith", TenantId = "tenant-1" });

        // Assert
        var text = sink.Events[0].RenderMessage();
        text.Should().NotContain("Alice Smith");
        // TenantId não é PII, não deve ser mascarado
        text.Should().Contain("tenant-1");
    }

    [Fact]
    public void Log_WhenTenantIdAndCorrelationIdPresent_PassesThroughUnchanged()
    {
        // Arrange
        var sink = new CaptureSink();
        var logger = new Serilog.LoggerConfiguration()
            .Destructure.With<MaskEmailDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var tenantId = Guid.NewGuid().ToString();
        var correlationId = Guid.NewGuid().ToString();

        // Act — campos de rastreabilidade devem passar intactos
        logger.Information("Request processed. TenantId: {TenantId}, CorrelationId: {CorrelationId}",
            tenantId, correlationId);

        // Assert
        var text = sink.Events[0].RenderMessage();
        text.Should().Contain(tenantId);
        text.Should().Contain(correlationId);
    }

    [Fact]
    public void MaskEmailPolicy_WhenEmailInObject_IsNotExposedInStructuredProperty()
    {
        // Arrange — e-mail aninhado em objeto estruturado
        var sink = new CaptureSink();
        var logger = new Serilog.LoggerConfiguration()
            .Destructure.With<MaskEmailDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        // Act
        logger.Information("User action: {@User}", new { Email = "bob@example.com", TenantId = "t1" });

        // Assert — e-mail mascarado na propriedade estruturada (não no template de mensagem)
        sink.Events.Should().HaveCount(1);
        // A propriedade User deve ser uma estrutura com Email mascarado
        var userProp = sink.Events[0].Properties["User"];
        var userText = userProp.ToString();
        userText.Should().NotContain("bob@example.com");
        // TenantId não é PII, deve aparecer
        userText.Should().Contain("t1");
    }
}
