using System.Text.Json.Serialization;

namespace Digest.Contracts;

/// <summary>
/// DTO de resposta 202 Accepted do trigger <c>POST /internal/digest/trigger</c> (design §8.1, RNF 8.1).
/// Devolvido ANTES da conclusão dos envios: o endpoint apenas valida, seleciona tenants e enfileira o fan-out.
/// </summary>
public sealed class TriggerResponse
{
    /// <summary>Sempre <c>true</c> quando o trigger foi aceito (202).</summary>
    [JsonPropertyName("accepted")]
    public bool Accepted { get; init; } = true;

    /// <summary>Número de tenants elegíveis enfileirados para processamento.</summary>
    [JsonPropertyName("eligible_tenants")]
    public int EligibleTenants { get; init; }

    /// <summary>Identificador de correlação desta execução do trigger (sem PII).</summary>
    [JsonPropertyName("correlation_id")]
    public Guid CorrelationId { get; init; }
}
