using FluentAssertions;
using PartnerManagement.Infrastructure.Audit;
using Xunit;

namespace PartnerManagement.Infrastructure.Tests.Audit;

/// <summary>
/// Scan anti-PII — gate CI obrigatório (categoria <c>PiiScan</c>).
/// Verifica que <see cref="PartnerPiiMasker"/> remove corretamente PII de contato de payloads JSON
/// e que <see cref="PartnerPiiMasker.ContainsPii"/> detecta vazamentos de e-mail/telefone.
/// <c>partner.name</c> <strong>não é PII</strong> por decisão VAL-PARTNER-01 (2026-06-15):
/// pode aparecer em claro e não é detectado como violação.
/// O teste de log do handler (criação de parceiro) vive em
/// <c>PartnerManagement.Application.Tests</c>, pois <c>CreatePartnerHandler</c> é internal.
///
/// Mapeia: TASK-27, RNF 4, design §11, RISK-PM-02.
/// </summary>
[Trait("Category", "PiiScan")]
public sealed class AntiPiiScanTests
{
    // PII de teste com valores realistas
    private const string KnownName = "Maria Aparecida da Silva";
    private const string KnownEmail = "maria.aparecida@empresa.com.br";
    private const string KnownPhone = "11988887777";

    private static readonly PartnerPiiMasker Masker = new();

    // =========================================================================
    // TASK-27: PartnerPiiMasker.MaskJson garante ausência de PII de contato após mascaramento
    // =========================================================================

    [Fact(DisplayName = "TASK-27: MaskJson aplicado ao delta_json remove PII de contato (name permanece em claro)")]
    public void MaskJson_Applied_ToFullDeltaPayload_RemovesContactPii()
    {
        // Arrange — payload típico de auditoria com todos os campos
        string deltaJson = $$$"""
            {
                "name": "{{{KnownName}}}",
                "contact_email": "{{{KnownEmail}}}",
                "contact_phone": "{{{KnownPhone}}}",
                "partner_type": "Indicador",
                "pct_setup": "10.00",
                "pct_recorrente": "5.00",
                "active": true
            }
            """;

        // Act
        string masked = Masker.MaskJson(deltaJson);

        // Assert — PII de contato mascarada; name permanece em claro (VAL-PARTNER-01)
        bool containsContactPii = Masker.ContainsPii(masked, KnownEmail, KnownPhone);
        containsContactPii.Should().BeFalse(
            "após MaskJson, nenhuma PII de contato deve estar em texto claro no delta_json de auditoria");
        masked.Should().Contain(PartnerPiiMasker.MaskedEmail, "contact_email deve estar mascarado");
        masked.Should().NotContain(KnownEmail, "e-mail original não pode estar no output");
        masked.Should().NotContain(KnownPhone, "telefone original não pode estar no output");

        // name aparece em claro — comportamento esperado por VAL-PARTNER-01
        masked.Should().Contain(KnownName, "name não é PII e deve permanecer em claro (VAL-PARTNER-01)");
    }

    // =========================================================================
    // TASK-27: Outbox payload de evento não contém PII de contato
    // =========================================================================

    [Fact(DisplayName = "TASK-27: Payload de evento de auditoria não contém PII de contato após mascaramento")]
    public void AuditPayload_AfterMasking_ContainsNoContactPii()
    {
        // Arrange — simular um payload de evento que inclui contato (erro de implementação)
        string originalPayload = $$$"""
            {
                "entityType": "Partner",
                "entityId": "00000000-0000-0000-0000-000000000001",
                "tenantId": "00000000-0000-0000-0000-000000000002",
                "action": "Create",
                "deltaJson": {
                    "name": "{{{KnownName}}}",
                    "contact_email": "{{{KnownEmail}}}"
                },
                "correlationId": "abc-123"
            }
            """;

        // Act — aplicar mascaramento antes de persistir no Outbox
        string maskedPayload = Masker.MaskJson(originalPayload);

        // Assert
        Masker.ContainsPii(maskedPayload, KnownEmail).Should().BeFalse(
            "payload de evento de auditoria não pode conter PII de contato em texto claro");
    }

    // =========================================================================
    // TASK-27: Verificação heurística de e-mail em texto livre
    // =========================================================================

    [Fact(DisplayName = "TASK-27: ContainsPii detecta e-mail em texto livre (heurística)")]
    public void ContainsPii_DetectsEmailInFreeText()
    {
        // Simula log com e-mail escapado acidentalmente em texto livre
        string logMessage = $"Criando parceiro com contato {KnownEmail}";

        Masker.ContainsPii(logMessage, knownEmail: KnownEmail).Should().BeTrue(
            "ContainsPii deve detectar e-mail em texto livre para que o gate funcione");
    }

    [Fact(DisplayName = "TASK-27: Texto com name em claro não aciona o gate (VAL-PARTNER-01)")]
    public void ContainsPii_TextWithNameOnly_DoesNotTrigger()
    {
        // name em claro é permitido por VAL-PARTNER-01
        string logMessage = $"Parceiro {KnownName} criado no tenant {Guid.NewGuid()}";

        Masker.ContainsPii(logMessage, KnownEmail, KnownPhone).Should().BeFalse(
            "name em claro não é PII por VAL-PARTNER-01");
    }

    [Fact(DisplayName = "TASK-27: Texto sem PII não aciona o gate")]
    public void ContainsPii_CleanText_DoesNotTrigger()
    {
        // Simula log seguro (apenas IDs e categorias)
        string logMessage = $"Parceiro {Guid.NewGuid()} criado no tenant {Guid.NewGuid()}";

        Masker.ContainsPii(logMessage, KnownEmail, KnownPhone).Should().BeFalse(
            "texto com IDs não contém PII");
    }

    [Fact(DisplayName = "TASK-27: MaskJson não altera campos não-PII")]
    public void MaskJson_DoesNotAlterNonPiiFields()
    {
        // Arrange
        string deltaJson = """
            {
                "partner_type": "Indicador",
                "pct_setup": "10.00",
                "active": true
            }
            """;

        // Act
        string masked = Masker.MaskJson(deltaJson);

        // Assert — campos sem PII devem permanecer intactos
        masked.Should().Contain("Indicador", "partner_type não é PII e não deve ser mascarado");
        masked.Should().Contain("10.00", "pct_setup não é PII e não deve ser mascarado");
    }
}
