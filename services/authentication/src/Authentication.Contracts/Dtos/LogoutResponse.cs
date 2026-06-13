using System.Text.Json.Serialization;

namespace Authentication.Contracts.Dtos;

/// <summary>
/// Resposta do endpoint <c>POST /v1/auth/logout</c>.
///
/// Idempotente: repetições retornam o mesmo status (Req 9.5, PBT-04).
///
/// Mapeia: design.md § 8.2, Req 9, TASK-17.
/// </summary>
public sealed class LogoutResponse
{
    /// <summary>Status da operação. Sempre <c>"revoked"</c> em sucesso.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "revoked";
}
