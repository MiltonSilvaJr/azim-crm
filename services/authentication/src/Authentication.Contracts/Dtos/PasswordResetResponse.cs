using System.Text.Json.Serialization;

namespace Authentication.Contracts.Dtos;

/// <summary>
/// Resposta do endpoint <c>POST /v1/auth/password-reset</c>.
///
/// Sempre retornada com HTTP 202 e status <c>"accepted"</c>, independente
/// de o e-mail existir ou não (anti-enumeração — PBT-03, Req 8.3, Req 10.2).
///
/// Mapeia: design.md § 8.5, Req 8, PBT-03, TASK-17.
/// </summary>
public sealed class PasswordResetResponse
{
    /// <summary>Status da operação. Sempre <c>"accepted"</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "accepted";
}
