using System.Text.Json.Serialization;

namespace Authentication.Contracts.Dtos;

/// <summary>
/// Requisição do endpoint <c>POST /v1/auth/password-reset</c>.
///
/// Endpoint público. A resposta é sempre idêntica (202 accepted) independente
/// de o e-mail existir ou não (anti-enumeração — PBT-03, Req 8.3, Req 10.2).
///
/// Mapeia: design.md § 8.5, Req 8, PBT-03, TASK-17.
/// </summary>
public sealed class PasswordResetRequest
{
    /// <summary>E-mail que solicita a redefinição de senha.</summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}
