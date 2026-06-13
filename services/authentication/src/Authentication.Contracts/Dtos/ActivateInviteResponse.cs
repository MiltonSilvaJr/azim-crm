using System.Text.Json.Serialization;

namespace Authentication.Contracts.Dtos;

/// <summary>
/// Resposta do endpoint <c>POST /v1/auth/invites/activate</c>.
///
/// Retornada com HTTP 200 em ativação bem-sucedida (design.md § 8.4).
///
/// Mapeia: design.md § 8.4, Req 7, TASK-17.
/// </summary>
public sealed class ActivateInviteResponse
{
    /// <summary>Status da operação. Sempre <c>"activated"</c> em sucesso.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "activated";
}
