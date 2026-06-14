namespace ActivityManagement.Infrastructure.Tests.Security;

using ActivityManagement.Infrastructure.Audit;
using FluentAssertions;
using Xunit;

/// <summary>
/// Scan anti-PII: verifica que title e description nunca aparecem sem mascaramento
/// em logs, delta_json de auditoria ou payloads de evento (TASK-23, RNF 7, DD-009).
///
/// Mapeia: TASK-23, RNF 7.2, DD-009, design §11.
/// </summary>
public sealed class AntiPiiScanTests
{
    // ── PiiMasker: title mascarado ────────────────────────────────────────────

    [Fact]
    public void PiiMasker_MaskDelta_MasksTitle()
    {
        // Arrange
        var delta = new Dictionary<string, object?>
        {
            ["title"]  = "Reunião confidencial",
            ["status"] = "completed",
        };

        // Act
        var result = PiiMasker.MaskDelta(delta);

        // Assert: title deve ser mascarado
        result["title"].Should().Be(PiiMasker.MaskedValue,
            because: "title é PII potencial e deve ser mascarado (RNF 7.2)");
        result["status"].Should().Be("completed",
            because: "campos não-PII devem ser preservados sem alteração");
    }

    [Fact]
    public void PiiMasker_MaskDelta_MasksDescription()
    {
        // Arrange
        var delta = new Dictionary<string, object?>
        {
            ["description"] = "Conteúdo confidencial do cliente",
            ["type"]        = "meeting",
        };

        // Act
        var result = PiiMasker.MaskDelta(delta);

        // Assert
        result["description"].Should().Be(PiiMasker.MaskedValue,
            because: "description é PII potencial e deve ser mascarado");
        result["type"].Should().Be("meeting");
    }

    [Fact]
    public void PiiMasker_MaskDelta_CaseInsensitive_MasksTitleInUpperCase()
    {
        // Arrange: verificar que a comparação é insensível a maiúsculas
        var delta = new Dictionary<string, object?>
        {
            ["Title"]  = "Reunião",
            ["DESCRIPTION"] = "Descrição",
        };

        // Act
        var result = PiiMasker.MaskDelta(delta);

        // Assert
        result["Title"].Should().Be(PiiMasker.MaskedValue);
        result["DESCRIPTION"].Should().Be(PiiMasker.MaskedValue);
    }

    // ── PiiMasker: JSON mascarado ─────────────────────────────────────────────

    [Fact]
    public void PiiMasker_MaskJson_MasksTitle()
    {
        // Arrange
        const string json = """{"title":"Reunião com cliente","status":"pending"}""";

        // Act
        var masked = PiiMasker.MaskJson(json);

        // Assert: title deve ser [MASKED], status preservado
        masked.Should().Contain("[MASKED]");
        masked.Should().NotContain("Reunião com cliente");
        masked.Should().Contain("pending");
    }

    [Fact]
    public void PiiMasker_MaskJson_MasksDescription()
    {
        // Arrange
        const string json = """{"description":"Informação confidencial","owner_id":"abc123"}""";

        // Act
        var masked = PiiMasker.MaskJson(json);

        // Assert
        masked.Should().Contain("[MASKED]");
        masked.Should().NotContain("Informação confidencial");
        masked.Should().Contain("abc123");
    }

    [Fact]
    public void PiiMasker_MaskJson_EmptyOrNull_ReturnsInput()
    {
        // Arrange/Act/Assert: JSON vazio ou nulo retorna sem modificação (sem crash)
        PiiMasker.MaskJson("").Should().Be("");
        PiiMasker.MaskJson("   ").Should().Be("   ");
    }

    [Fact]
    public void PiiMasker_MaskJson_InvalidJson_ReturnsMaxkedValue()
    {
        // Arrange: JSON inválido deve ser tratado defensivamente
        const string invalidJson = "{ not valid json }";

        // Act
        var result = PiiMasker.MaskJson(invalidJson);

        // Assert: segurança defensiva — JSON inválido deve retornar valor mascarado
        result.Should().Be(PiiMasker.MaskedValue,
            because: "JSON inválido deve ser mascarado por segurança defensiva");
    }

    // ── Scan: nenhum campo PII sem mascaramento em delta_json ─────────────────

    [Theory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("Title")]
    [InlineData("TITLE")]
    [InlineData("Description")]
    public void PiiMasker_MaskDelta_AlwaysMasksPiiFields(string fieldName)
    {
        // Arrange
        var delta = new Dictionary<string, object?>
        {
            [fieldName] = "Dado sensível que não deve aparecer",
            ["id"]      = Guid.NewGuid(),
        };

        // Act
        var result = PiiMasker.MaskDelta(delta);

        // Assert: campo PII sempre mascarado independente do case
        result[fieldName].Should().Be(PiiMasker.MaskedValue,
            because: $"campo '{fieldName}' é PII e deve sempre ser mascarado");
    }

    [Theory]
    [InlineData("id")]
    [InlineData("status")]
    [InlineData("type")]
    [InlineData("owner_id")]
    [InlineData("tenant_id")]
    [InlineData("due_at")]
    [InlineData("action")]
    public void PiiMasker_MaskDelta_PreservesNonPiiFields(string fieldName)
    {
        // Arrange
        const string value = "valor-publico";
        var delta = new Dictionary<string, object?> { [fieldName] = value };

        // Act
        var result = PiiMasker.MaskDelta(delta);

        // Assert: campos não-PII preservados
        result[fieldName].Should().Be(value,
            because: $"campo '{fieldName}' não é PII e deve ser preservado");
    }
}
