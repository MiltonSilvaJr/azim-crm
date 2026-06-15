using FluentAssertions;
using PartnerManagement.Infrastructure.Audit;
using Xunit;

namespace PartnerManagement.Infrastructure.Tests.Audit;

/// <summary>
/// Testes unitários para <see cref="PartnerPiiMasker"/> (TASK-20).
/// Verifica: mascaramento de e-mail, telefone, JSON estruturado e detecção de PII.
/// <c>partner.name</c> não é PII por decisão VAL-PARTNER-01 (2026-06-15) — não é mascarado.
/// Mapeia: RNF 4, design §6.6, design §11, TASK-20.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PartnerPiiMaskerTests
{
    private readonly PartnerPiiMasker _masker = new();

    // =========================================================================
    // MaskEmail
    // =========================================================================

    [Fact(DisplayName = "TASK-20: MaskEmail mascara qualquer string")]
    public void MaskEmail_AlwaysReturnsMaskedConstant()
    {
        _masker.MaskEmail("user@empresa.com.br").Should().Be(PartnerPiiMasker.MaskedEmail);
        _masker.MaskEmail(null).Should().Be(PartnerPiiMasker.MaskedEmail);
    }

    // =========================================================================
    // MaskPhone
    // =========================================================================

    [Fact(DisplayName = "TASK-20: MaskPhone mascara qualquer string")]
    public void MaskPhone_AlwaysReturnsMaskedConstant()
    {
        _masker.MaskPhone("+5511987654321").Should().Be(PartnerPiiMasker.MaskedPhone);
        _masker.MaskPhone(null).Should().Be(PartnerPiiMasker.MaskedPhone);
    }

    // =========================================================================
    // MaskJson
    // =========================================================================

    [Fact(DisplayName = "VAL-PARTNER-01: MaskJson NÃO mascara campo 'name' — name não é PII")]
    public void MaskJson_DoesNotMaskNameField()
    {
        string json = """{"name":"João Silva","partner_type":"Indicador"}""";
        string result = _masker.MaskJson(json);

        result.Should().Contain("João Silva", "name não é PII por VAL-PARTNER-01 e deve permanecer em claro");
    }

    [Fact(DisplayName = "TASK-20: MaskJson mascara campo 'contact_email' em JSON")]
    public void MaskJson_MasksContactEmailField()
    {
        string json = """{"contact_email":"joao@empresa.com","active":true}""";
        string result = _masker.MaskJson(json);

        result.Should().NotContain("joao@empresa.com");
        result.Should().Contain(PartnerPiiMasker.MaskedEmail);
    }

    [Fact(DisplayName = "TASK-20: MaskJson mascara campo 'contact_phone' em JSON")]
    public void MaskJson_MasksContactPhoneField()
    {
        string json = """{"contact_phone":"+5511999999999","active":true}""";
        string result = _masker.MaskJson(json);

        result.Should().NotContain("+5511999999999");
        result.Should().Contain(PartnerPiiMasker.MaskedPhone);
    }

    [Fact(DisplayName = "TASK-20: MaskJson mascara e-mail residual fora de campos conhecidos")]
    public void MaskJson_MasksResidualEmailPattern()
    {
        string json = """{"notes":"contato: admin@empresa.com.br para dúvidas"}""";
        string result = _masker.MaskJson(json);

        result.Should().NotContain("admin@empresa.com.br");
        result.Should().Contain(PartnerPiiMasker.MaskedEmail);
    }

    [Fact(DisplayName = "TASK-20: MaskJson preserva campos não-PII")]
    public void MaskJson_PreservesNonPiiFields()
    {
        string json = """{"id":"abc-123","partner_type":"Indicador","active":true}""";
        string result = _masker.MaskJson(json);

        result.Should().Contain("abc-123");
        result.Should().Contain("Indicador");
        result.Should().Contain("true");
    }

    [Fact(DisplayName = "TASK-20: MaskJson retorna string vazia quando input vazio")]
    public void MaskJson_ReturnsEmpty_WhenInputEmpty()
    {
        _masker.MaskJson(string.Empty).Should().BeEmpty();
    }

    // =========================================================================
    // ContainsPii
    // =========================================================================

    [Fact(DisplayName = "VAL-PARTNER-01: ContainsPii NÃO detecta name em claro como violação")]
    public void ContainsPii_DoesNotDetectNameAsViolation()
    {
        // name em claro é permitido por VAL-PARTNER-01
        bool result = _masker.ContainsPii(
            "Parceiro Acme Corp foi criado");

        result.Should().BeFalse("name em claro não é PII por VAL-PARTNER-01");
    }

    [Fact(DisplayName = "TASK-20: ContainsPii detecta e-mail em claro")]
    public void ContainsPii_DetectsKnownEmail()
    {
        bool result = _masker.ContainsPii(
            """{"contact_email":"user@empresa.com"}""",
            knownEmail: "user@empresa.com");

        result.Should().BeTrue();
    }

    [Fact(DisplayName = "TASK-20: ContainsPii detecta padrão de e-mail sem referência conhecida")]
    public void ContainsPii_DetectsEmailPattern()
    {
        bool result = _masker.ContainsPii("email: qualquer@dominio.com");
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "TASK-20: ContainsPii retorna false para texto sem PII")]
    public void ContainsPii_ReturnsFalse_ForCleanText()
    {
        bool result = _masker.ContainsPii(
            """{"id":"abc","partner_type":"Indicador","active":true}""");

        result.Should().BeFalse();
    }

    [Fact(DisplayName = "TASK-20: ContainsPii retorna false para string vazia")]
    public void ContainsPii_ReturnsFalse_ForEmptyString()
    {
        _masker.ContainsPii(string.Empty).Should().BeFalse();
    }

    [Fact(DisplayName = "TASK-20: MaskJson seguido de ContainsPii não detecta PII de contato")]
    public void MaskJson_ThenContainsPii_ReturnsFalse()
    {
        // Verifica o gate anti-PII: após mascaramento, ContainsPii não deve detectar PII de contato.
        // name aparece em claro no output pois não é PII (VAL-PARTNER-01).
        string originalJson = """
            {
                "name": "Maria Aparecida",
                "contact_email": "maria@empresa.com.br",
                "contact_phone": "+5511988887777"
            }
            """;

        string masked = _masker.MaskJson(originalJson);

        bool containsPii = _masker.ContainsPii(
            masked,
            knownEmail: "maria@empresa.com.br",
            knownPhone: "+5511988887777");

        containsPii.Should().BeFalse("após MaskJson, nenhuma PII de contato deve estar em claro");
    }
}
