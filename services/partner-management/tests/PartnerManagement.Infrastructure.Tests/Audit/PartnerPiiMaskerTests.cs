using FluentAssertions;
using PartnerManagement.Infrastructure.Audit;
using Xunit;

namespace PartnerManagement.Infrastructure.Tests.Audit;

/// <summary>
/// Testes unitários para <see cref="PartnerPiiMasker"/> (TASK-20).
/// Verifica: mascaramento de nome, e-mail, telefone, JSON estruturado e detecção de PII.
/// Mapeia: RNF 4, DD-008, design §6.6, design §11, TASK-20.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PartnerPiiMaskerTests
{
    private readonly PartnerPiiMasker _masker = new();

    // =========================================================================
    // MaskName
    // =========================================================================

    [Fact(DisplayName = "TASK-20: MaskName mascara qualquer string")]
    public void MaskName_AlwaysReturnsMaskedConstant()
    {
        _masker.MaskName("Acme Corp").Should().Be(PartnerPiiMasker.MaskedName);
        _masker.MaskName(null).Should().Be(PartnerPiiMasker.MaskedName);
        _masker.MaskName(string.Empty).Should().Be(PartnerPiiMasker.MaskedName);
    }

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

    [Fact(DisplayName = "TASK-20: MaskJson mascara campo 'name' em JSON")]
    public void MaskJson_MasksNameField()
    {
        string json = """{"name":"João Silva","partner_type":"Indicador"}""";
        string result = _masker.MaskJson(json);

        result.Should().NotContain("João Silva");
        result.Should().Contain(PartnerPiiMasker.MaskedName);
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

    [Fact(DisplayName = "TASK-20: ContainsPii detecta nome em claro")]
    public void ContainsPii_DetectsKnownName()
    {
        bool result = _masker.ContainsPii(
            "Parceiro Acme Corp foi criado",
            knownName: "Acme Corp");

        result.Should().BeTrue();
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

    [Fact(DisplayName = "TASK-20: MaskJson seguido de ContainsPii não detecta PII")]
    public void MaskJson_ThenContainsPii_ReturnsFalse()
    {
        // Verifica o gate anti-PII: após mascaramento, ContainsPii não deve detectar
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
            knownName: "Maria Aparecida",
            knownEmail: "maria@empresa.com.br",
            knownPhone: "+5511988887777");

        containsPii.Should().BeFalse("após MaskJson, nenhuma PII deve estar em claro");
    }
}
