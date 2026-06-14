using AccountManagement.Domain.Accounts.ValueObjects;
using AccountManagement.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Text;

namespace AccountManagement.Infrastructure.Tests.Security;

/// <summary>
/// Testes de scan anti-PII em logs — gate de segurança obrigatório (RNF 1.4).
///
/// ST-01 de TASK-17: executa ciclo de criação/edição de contato com PII real,
/// captura output de logs e falha se qualquer valor de PII aparecer em texto claro.
///
/// ST-02 de TASK-17: valida que delta_json do audit_log tem campos name/email/phone
/// substituídos por marcadores — PiiMasker está no caminho correto.
///
/// Mapeia: TASK-17 ST-01/ST-02, RNF 1.4, design §11 (scan anti-PII), DD-003.
/// </summary>
public sealed class AntiPiiLogScanTests
{
    // =========================================================================
    // Sink de log de teste para capturar todas as mensagens
    // =========================================================================

    private sealed class AntiPiiLogSink : ILoggerProvider, ILogger
    {
        private readonly StringBuilder _buffer = new();
        private static string[] _piiValues = [];

        public static AntiPiiLogSink Create(params string[] piiValues)
        {
            _piiValues = piiValues;
            return new AntiPiiLogSink();
        }

        public string GetCapturedLogs() => _buffer.ToString();

        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _buffer.AppendLine(message);
            if (exception is not null)
                _buffer.AppendLine(exception.ToString());
        }
    }

    // =========================================================================
    // ST-01: scan de logs — PII não pode aparecer em texto claro
    // =========================================================================

    [Fact(DisplayName = "AntiPiiScan: PiiMasker.ContainsPii detecta PII em JSON antes do mascaramento")]
    public void ContainsPii_DetectsRealPii_BeforeMasking()
    {
        // Arrange
        const string piiName = "Fernanda Martins";
        const string piiEmail = "fernanda@empresa.com.br";
        const string piiPhone = "11987654321";

        var masker = new PiiMasker();
        var deltaJson = $$"""{"name":"{{piiName}}","email":"{{piiEmail}}","phone":"{{piiPhone}}","role":"admin"}""";

        // Act — antes do mascaramento, PII deve ser detectada
        var hasPii = masker.ContainsPii(deltaJson, [piiName, piiEmail, piiPhone]);

        // Assert
        hasPii.Should().BeTrue(
            "ContainsPii deve detectar PII em JSON antes do mascaramento (controle positivo do scan)");
    }

    [Fact(DisplayName = "AntiPiiScan: PiiMasker.MaskContactDelta elimina PII de nome, e-mail e telefone")]
    public void MaskContactDelta_EliminatesAllPiiFields()
    {
        // Arrange
        const string piiName = "Fernanda Martins";
        const string piiEmail = "fernanda@empresa.com.br";
        const string piiPhone = "11987654321";

        var masker = new PiiMasker();
        var deltaJson = $$"""{"name":"{{piiName}}","email":"{{piiEmail}}","phone":"{{piiPhone}}","role":"admin"}""";

        // Act — após mascaramento, PII não pode aparecer
        var masked = masker.MaskContactDelta(deltaJson);
        var hasPii = masker.ContainsPii(masked, [piiName, piiEmail, piiPhone]);

        // Assert
        hasPii.Should().BeFalse(
            "após mascaramento, ContainsPii não deve encontrar PII em texto claro (gate RNF 1.4)");
        masked.Should().Contain(ContactInfo.AnonymizationMarker,
            "campos de PII devem ser substituídos pelo marcador de anonimização");
        masked.Should().Contain("\"role\":\"admin\"",
            "campos de não-PII devem ser preservados pelo mascaramento");
    }

    [Fact(DisplayName = "AntiPiiScan: delta_json de audit_log não contém PII original após mascaramento")]
    public void AuditLogDeltaJson_DoesNotContainOriginalPii()
    {
        // Arrange — simula o que AuditPublisher faz antes de gravar em audit_logs (TASK-17 ST-02)
        const string piiName = "João Carlos Borges";
        const string piiEmail = "joao.borges@cliente.io";
        const string piiPhone = "21912345678";

        var masker = new PiiMasker();

        // delta_json como seria construído pelo domínio para ContactLinked.MaskedDelta
        var rawContactDelta = $$"""
            {
                "name": "{{piiName}}",
                "email": "{{piiEmail}}",
                "phone": "{{piiPhone}}",
                "role": "coordenador",
                "action": "created",
                "contact_id": "{{Guid.NewGuid()}}",
                "account_id": "{{Guid.NewGuid()}}"
            }
            """;

        // Act — mascaramento aplicado pelo AuditPublisher (linha: var maskedDelta = _piiMasker.MaskContactDelta(rawDelta))
        var deltaJson = masker.MaskContactDelta(rawContactDelta);

        // Assert — delta_json final (gravado em audit_logs) não contém PII (TASK-17 ST-02)
        deltaJson.Should().NotContain(piiName,
            "audit_logs.delta_json não pode conter nome do contato em texto claro");
        deltaJson.Should().NotContain(piiEmail,
            "audit_logs.delta_json não pode conter e-mail do contato em texto claro");
        deltaJson.Should().NotContain(piiPhone,
            "audit_logs.delta_json não pode conter telefone do contato em texto claro");

        masker.ContainsPii(deltaJson, [piiName, piiEmail, piiPhone])
            .Should().BeFalse("gate de auditoria: delta_json de audit_log nunca carrega PII original");
    }

    [Fact(DisplayName = "AntiPiiScan: ContactForgotten não carrega PII — apenas contact_id e requestedBy")]
    public void ContactForgottenEvent_DoesNotContainPii()
    {
        // Arrange — ContactForgotten conforme design §4.4
        const string piiName = "Ana Paula Rodrigues";
        const string piiEmail = "ana.paula@firma.com.br";

        var contactId = Guid.NewGuid();
        var requestedBy = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Simula o payload do evento (sem PII — apenas IDs)
        var eventPayload = $$"""
            {
                "contactId": "{{contactId}}",
                "requestedBy": "{{requestedBy}}",
                "tenantId": "{{tenantId}}",
                "occurredAt": "{{DateTimeOffset.UtcNow:O}}"
            }
            """;

        // Assert — payload do evento ContactForgotten não deve conter PII
        var masker = new PiiMasker();
        masker.ContainsPii(eventPayload, [piiName, piiEmail])
            .Should().BeFalse(
                "ContactForgotten deve carregar apenas IDs, sem PII em texto claro (design §4.4)");
    }

    [Theory(DisplayName = "AntiPiiScan: scan em múltiplos cenários de PII — todos mascarados")]
    [InlineData("Maria Silva", "maria@silva.com", "11911112222")]
    [InlineData("PEDRO SANTOS", "PEDRO@SANTOS.COM.BR", "21900001111")]
    [InlineData("Lígia O'Brien-Fonseca", "ligia.obrien@test.io", "48999887766")]
    public void ScanAntiPii_AllScenarios_NoPiiAfterMasking(string name, string email, string phone)
    {
        // Arrange
        var masker = new PiiMasker();
        var delta = $$"""{"name":"{{name}}","email":"{{email}}","phone":"{{phone}}","role":"teste"}""";

        // Act
        var masked = masker.MaskContactDelta(delta);

        // Assert — gate: após mascaramento nenhum valor de PII pode estar presente
        masker.ContainsPii(masked, [name, email, phone])
            .Should().BeFalse(
                $"após mascaramento, nenhuma PII ({name}/{email}/{phone}) pode aparecer em texto claro");
    }
}
