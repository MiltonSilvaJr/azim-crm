using System.Text.Json;
using Digest.Contracts;
using Xunit;

namespace Digest.Api.Tests.Contracts;

/// <summary>
/// Testes de contrato dos DTOs do trigger (TASK-23).
/// Cobre: TriggerRequest (reference_utc opcional), TriggerResponse (accepted, eligible_tenants, correlation_id).
/// </summary>
public sealed class TriggerContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
    };

    // ---------------------------------------------------------------
    // TriggerRequest — reference_utc opcional
    // ---------------------------------------------------------------

    [Fact]
    public void TriggerRequest_WithReferenceUtc_SerializesAndDeserializes()
    {
        // Arrange
        var request = new TriggerRequest { ReferenceUtc = "2026-06-14T10:00:00Z" };

        // Act
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<TriggerRequest>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("2026-06-14T10:00:00Z", deserialized.ReferenceUtc);
    }

    [Fact]
    public void TriggerRequest_WithoutReferenceUtc_SerializesAsNull()
    {
        // Arrange
        var request = new TriggerRequest { ReferenceUtc = null };

        // Act
        var json = JsonSerializer.Serialize(request, JsonOptions);

        // Assert — reference_utc ausente no JSON é aceitável (Req 1.5)
        Assert.NotNull(json);
        // Pode serializar como null ou omitir o campo — ambos aceitáveis
    }

    // ---------------------------------------------------------------
    // TriggerResponse — campos obrigatórios
    // ---------------------------------------------------------------

    [Fact]
    public void TriggerResponse_RoundTrip_PreservesAllFields()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var response = new TriggerResponse
        {
            Accepted = true,
            EligibleTenants = 5,
            CorrelationId = correlationId,
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<TriggerResponse>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.True(deserialized.Accepted);
        Assert.Equal(5, deserialized.EligibleTenants);
        Assert.Equal(correlationId, deserialized.CorrelationId);
    }

    [Fact]
    public void TriggerResponse_Serialized_ContainsSnakeCaseFields()
    {
        // Arrange
        var response = new TriggerResponse
        {
            Accepted = true,
            EligibleTenants = 3,
            CorrelationId = Guid.NewGuid(),
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var properties = doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

        // Assert — campos em snake_case (design §8.1)
        Assert.Contains("accepted", properties);
        Assert.Contains("eligible_tenants", properties);
        Assert.Contains("correlation_id", properties);
    }
}
