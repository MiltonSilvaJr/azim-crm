using System.Text.Json.Serialization;

namespace Authentication.Contracts.Dtos;

/// <summary>
/// Requisição do endpoint <c>POST /v1/auth/invites/activate</c>.
///
/// Endpoint público (sem Bearer). O token de ativação é extraído do link
/// enviado por e-mail ao usuário convidado.
///
/// Mapeia: design.md § 8.4, Req 7.4, PBT-05, TASK-17.
/// </summary>
public sealed class ActivateInviteRequest
{
    /// <summary>Token de ativação recebido no link de convite.</summary>
    [JsonPropertyName("activation_token")]
    public string ActivationToken { get; set; } = string.Empty;
}
