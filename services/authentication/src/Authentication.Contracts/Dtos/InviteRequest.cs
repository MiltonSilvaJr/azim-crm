using System.Text.Json.Serialization;

namespace Authentication.Contracts.Dtos;

/// <summary>
/// Requisição do endpoint <c>POST /v1/auth/invites</c>.
///
/// Requer papel <c>TenantAdmin</c>. O <c>tenant_id</c> é derivado do slug resolvido
/// pelo middleware — nunca aceito do chamador (design.md § 2, Req 1).
///
/// Mapeia: design.md § 8.3, Req 7, TASK-17.
/// </summary>
public sealed class InviteRequest
{
    /// <summary>E-mail do usuário a ser convidado.</summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Papel a ser atribuído ao usuário no tenant.</summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>Identificadores das unidades de negócio às quais o usuário será associado.</summary>
    [JsonPropertyName("bu_ids")]
    public IReadOnlyList<Guid> BuIds { get; set; } = [];
}
