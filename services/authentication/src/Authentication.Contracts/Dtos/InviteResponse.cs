using System.Text.Json.Serialization;

namespace Authentication.Contracts.Dtos;

/// <summary>
/// Resposta do endpoint <c>POST /v1/auth/invites</c>.
///
/// Retornada com HTTP 202 em criação bem-sucedida de convite (design.md § 8.3).
///
/// Mapeia: design.md § 8.3, Req 7, TASK-17.
/// </summary>
public sealed class InviteResponse
{
    /// <summary>Status da operação. Sempre <c>"invited"</c> em sucesso.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "invited";
}
