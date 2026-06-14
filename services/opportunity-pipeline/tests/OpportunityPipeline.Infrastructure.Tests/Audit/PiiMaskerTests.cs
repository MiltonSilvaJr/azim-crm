using OpportunityPipeline.Infrastructure.Audit;
using System.Text.Json;

namespace OpportunityPipeline.Infrastructure.Tests.Audit;

/// <summary>
/// Testes do PiiMasker — verifica mascaramento de campos PII em deltas de auditoria.
/// Sem PII em logs ou auditoria (RNF 6.3, RNF 10.4, TASK-16).
/// </summary>
public sealed class PiiMaskerTests
{
    [Theory(DisplayName = "PIIMASK_01: campos PII conhecidos devem ser mascarados")]
    [InlineData("email")]
    [InlineData("cpf")]
    [InlineData("nome")]
    [InlineData("name")]
    [InlineData("phone")]
    [InlineData("contact_name")]
    public void MaskAndSerialize_PiiFields_ShouldBeRedacted(string fieldName)
    {
        // Arrange
        var delta = JsonSerializer.Deserialize<Dictionary<string, object>>(
            $@"{{ ""{fieldName}"": ""dados_sensiveis"" }}");

        // Act
        var json = PiiMasker.MaskAndSerialize(delta);
        var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

        // Assert
        result[fieldName].GetString().Should().Be("[REDACTED]",
            $"o campo '{fieldName}' deve ser mascarado (RNF 6.3, RNF 10.4).");
    }

    [Fact(DisplayName = "PIIMASK_02: campos não-PII devem ser preservados")]
    public void MaskAndSerialize_NonPiiFields_ShouldBePreserved()
    {
        // Arrange
        var delta = new { action = "Create", opportunity_number = "AZ-0001", value_cents = 100000 };

        // Act
        var json = PiiMasker.MaskAndSerialize(delta);
        var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

        // Assert
        result["action"].GetString().Should().Be("Create");
        result["opportunity_number"].GetString().Should().Be("AZ-0001");
        result["value_cents"].GetInt64().Should().Be(100000);
    }

    [Fact(DisplayName = "PIIMASK_03: objeto null retorna JSON vazio")]
    public void MaskAndSerialize_NullDelta_ShouldReturnEmptyJson()
    {
        // Act
        var json = PiiMasker.MaskAndSerialize(null);

        // Assert
        json.Should().Be("{}");
    }

    [Fact(DisplayName = "PIIMASK_04: campo PII aninhado deve ser mascarado")]
    public void MaskAndSerialize_NestedPiiField_ShouldBeRedacted()
    {
        // Arrange
        var delta = new
        {
            opportunity_id = Guid.NewGuid(),
            contact = new { email = "user@test.com", contact_id = Guid.NewGuid() }
        };

        // Act
        var json = PiiMasker.MaskAndSerialize(delta);
        using var doc = JsonDocument.Parse(json);

        // Assert: email aninhado deve ser mascarado
        var emailValue = doc.RootElement
            .GetProperty("contact")
            .GetProperty("email")
            .GetString();

        emailValue.Should().Be("[REDACTED]",
            "campos PII aninhados também devem ser mascarados (RNF 6.3).");
    }
}
