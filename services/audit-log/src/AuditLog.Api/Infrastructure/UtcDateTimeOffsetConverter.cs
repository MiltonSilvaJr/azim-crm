using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuditLog.Api.Infrastructure;

/// <summary>
/// Conversor JSON que serializa <see cref="DateTimeOffset"/> como UTC ISO-8601 terminando em <c>Z</c>.
/// Garante que <c>createdAt</c> no contrato de resposta seja <c>2026-06-11T12:00:00Z</c>
/// e não <c>2026-06-11T12:00:00+00:00</c> (design §8.1, REQ-002.4).
/// </summary>
internal sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    /// <inheritdoc/>
    public override DateTimeOffset Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
        => DateTimeOffset.Parse(reader.GetString()!);

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        DateTimeOffset value,
        JsonSerializerOptions options)
    {
        // Normaliza para UTC antes de serializar, garantindo sufixo Z
        writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFZ"));
    }
}
