using System.Text.Json;
using Digest.Contracts;
using Digest.Contracts.Events;
using Xunit;

namespace Digest.Api.Tests.Contracts;

/// <summary>
/// Testes de contrato de <see cref="DigestEmailSentEvent"/> (TASK-23).
/// Cobre: round-trip JSON; campos obrigatórios; ausência de PII; tolerância a campos extras.
/// </summary>
public sealed class DigestEmailSentEventTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
    };

    // ---------------------------------------------------------------
    // Round-trip JSON — serialização e deserialização sem perda de dados
    // ---------------------------------------------------------------

    [Fact]
    public void DigestEmailSentEvent_RoundTrip_PreservesAllFields()
    {
        // Arrange
        var original = new DigestEmailSentEvent
        {
            Event = "digest.email_sent.v1",
            Version = "1",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DigestDate = "2026-06-14",
            MessageId = "postmark-msg-id-abc123",
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow.ToUniversalTime(),
        };

        // Act
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<DigestEmailSentEvent>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Event, deserialized.Event);
        Assert.Equal(original.Version, deserialized.Version);
        Assert.Equal(original.TenantId, deserialized.TenantId);
        Assert.Equal(original.UserId, deserialized.UserId);
        Assert.Equal(original.DigestDate, deserialized.DigestDate);
        Assert.Equal(original.MessageId, deserialized.MessageId);
        Assert.Equal(original.CorrelationId, deserialized.CorrelationId);
        Assert.Equal(original.CausationId, deserialized.CausationId);
        // OccurredAt: tolerância de 1 segundo para ajuste de formato ISO 8601
        Assert.InRange(
            (deserialized.OccurredAt - original.OccurredAt).Duration(),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1));
    }

    // ---------------------------------------------------------------
    // Campos do design §9.1 — exatamente os campos esperados
    // ---------------------------------------------------------------

    [Fact]
    public void DigestEmailSentEvent_Serialized_ContainsExactlyExpectedFields()
    {
        // Arrange
        var evt = new DigestEmailSentEvent
        {
            Event = "digest.email_sent.v1",
            Version = "1",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DigestDate = "2026-06-14",
            MessageId = "msg-id",
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
        };

        // Act
        var json = JsonSerializer.Serialize(evt, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var properties = doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

        // Assert — exatamente os campos do design §9.1
        Assert.Contains("event", properties);
        Assert.Contains("version", properties);
        Assert.Contains("tenant_id", properties);
        Assert.Contains("user_id", properties);
        Assert.Contains("digest_date", properties);
        Assert.Contains("message_id", properties);
        Assert.Contains("correlation_id", properties);
        Assert.Contains("causation_id", properties);
        Assert.Contains("occurred_at", properties);
    }

    // ---------------------------------------------------------------
    // Ausência de PII — sem e-mail, sem conteúdo (RNF 10.2, design §9.1)
    // ---------------------------------------------------------------

    [Fact]
    public void DigestEmailSentEvent_DoesNotContainPiiFields()
    {
        // O tipo não deve ter campos de e-mail, nome do usuário ou conteúdo do digest
        var properties = typeof(DigestEmailSentEvent).GetProperties();
        var propertyNames = properties.Select(p => p.Name.ToLowerInvariant()).ToHashSet();

        Assert.DoesNotContain("email", propertyNames);
        Assert.DoesNotContain("toemail", propertyNames);
        Assert.DoesNotContain("recipient", propertyNames);
        Assert.DoesNotContain("content", propertyNames);
        Assert.DoesNotContain("body", propertyNames);
        Assert.DoesNotContain("subject", propertyNames);
        Assert.DoesNotContain("name", propertyNames);
    }

    // ---------------------------------------------------------------
    // Tolerância retroativa — campo extra desconhecido é ignorado
    // ---------------------------------------------------------------

    [Fact]
    public void DigestEmailSentEvent_Deserialize_IgnoresUnknownFields()
    {
        // Arrange — JSON com campo extra que não existe no contrato
        var jsonWithExtraField = """
            {
              "event": "digest.email_sent.v1",
              "version": "1",
              "tenant_id": "11111111-1111-1111-1111-111111111111",
              "user_id": "22222222-2222-2222-2222-222222222222",
              "digest_date": "2026-06-14",
              "message_id": "msg-extra",
              "correlation_id": "33333333-3333-3333-3333-333333333333",
              "causation_id": "44444444-4444-4444-4444-444444444444",
              "occurred_at": "2026-06-14T10:00:00Z",
              "future_field_unknown": "some-value"
            }
            """;

        // Act — campo extra não deve lançar exceção (tolerância retroativa — design §9.1)
        var deserialized = JsonSerializer.Deserialize<DigestEmailSentEvent>(jsonWithExtraField, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("digest.email_sent.v1", deserialized.Event);
        Assert.Equal("1", deserialized.Version);
    }

    // ---------------------------------------------------------------
    // Nome do evento — versionamento por sufixo .v1
    // ---------------------------------------------------------------

    [Fact]
    public void DigestEmailSentEvent_EventName_IsVersioned()
    {
        var evt = new DigestEmailSentEvent { Event = "digest.email_sent.v1" };
        Assert.EndsWith(".v1", evt.Event);
    }
}
