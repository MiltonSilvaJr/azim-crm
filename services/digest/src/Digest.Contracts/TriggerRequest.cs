using System.Text.Json.Serialization;

namespace Digest.Contracts;

/// <summary>
/// DTO de request do trigger <c>POST /internal/digest/trigger</c> (design §8.1).
/// O campo <c>reference_utc</c> é opcional: ausência usa a hora cheia atual em UTC (Req 1.5).
/// </summary>
public sealed class TriggerRequest
{
    /// <summary>
    /// Instante UTC de referência do disparo (formato ISO 8601, ex.: <c>2026-06-14T10:00:00Z</c>).
    /// Opcional: se ausente, o worker usa a hora cheia atual em UTC (Req 1.5).
    /// </summary>
    [JsonPropertyName("reference_utc")]
    public string? ReferenceUtc { get; init; }
}
