using FluentAssertions;
using PartnerManagement.Infrastructure.Audit;
using Xunit;

namespace PartnerManagement.Infrastructure.Tests.Audit;

/// <summary>
/// Scan anti-PII — gate CI obrigatório (categoria <c>PiiScan</c>).
/// Verifica que <see cref="PartnerPiiMasker"/> remove corretamente PII de payloads JSON
/// e que <see cref="PartnerPiiMasker.ContainsPii"/> detecta vazamentos.
/// O teste de log do handler (criação de parceiro) vive em
/// <c>PartnerManagement.Application.Tests</c>, pois <c>CreatePartnerHandler</c> é internal.
///
/// Mapeia: TASK-27, RNF 4, DD-008, design §11, RISK-PM-02.
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
    // TASK-27: PartnerPiiMasker.MaskJson garante ausência de PII após mascaramento
    // =========================================================================

    [Fact(DisplayName = "TASK-27: MaskJson aplicado ao delta_json remove toda PII")]
    public void MaskJson_Applied_ToFullDeltaPayload_RemovesAllPii()
    {
        // Arrange — payload típico de auditoria com todos os campos PII
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

        // Assert
        bool containsPii = Masker.ContainsPii(masked, KnownName, KnownEmail, KnownPhone);
        containsPii.Should().BeFalse(
            "após MaskJson, nenhuma PII deve estar em texto claro no delta_json de auditoria");
        masked.Should().Contain(PartnerPiiMasker.MaskedName, "name deve estar mascarado");
        masked.Should().Contain(PartnerPiiMasker.MaskedEmail, "contact_email deve estar mascarado");
        masked.Should().NotContain(KnownName, "nome original não pode estar no output");
        masked.Should().NotContain(KnownEmail, "e-mail original não pode estar no output");
    }

    // =========================================================================
    // TASK-27: Outbox payload de evento não contém PII
    // =========================================================================

    [Fact(DisplayName = "TASK-27: Payload de evento de auditoria não contém PII após mascaramento")]
    public void AuditPayload_AfterMasking_ContainsNoPii()
    {
        // Arrange — simular um payload de evento que inclui nome (erro de implementação)
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
        Masker.ContainsPii(maskedPayload, KnownName, KnownEmail).Should().BeFalse(
            "payload de evento de auditoria não pode conter PII em texto claro");
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

    [Fact(DisplayName = "TASK-27: Texto sem PII não aciona o gate")]
    public void ContainsPii_CleanText_DoesNotTrigger()
    {
        // Simula log seguro (apenas IDs e categorias)
        string logMessage = $"Parceiro {Guid.NewGuid()} criado no tenant {Guid.NewGuid()}";

        Masker.ContainsPii(logMessage, KnownName, KnownEmail, KnownPhone).Should().BeFalse(
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
