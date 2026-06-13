using System.Text.Json.Serialization;

namespace Authentication.Contracts.Dtos;

/// <summary>
/// Resposta do endpoint <c>GET /v1/auth/me</c>.
///
/// Projeção do <c>AuthContext</c> injetado pelo middleware de autenticação.
/// Nunca expõe <c>identity_uid</c> nem qualquer claim do Identity Provider (DD-001, Req 11.4).
///
/// Mapeia: design.md § 8.1, Req 11, TASK-17.
/// </summary>
public sealed class MeResponse
{
    /// <summary>Identificador interno do usuário no tenant (UUID do módulo organization).</summary>
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }

    /// <summary>E-mail do usuário no tenant.</summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Papéis globais do usuário no tenant.</summary>
    [JsonPropertyName("roles")]
    public IReadOnlyList<string> Roles { get; set; } = [];

    /// <summary>Memberships do usuário (unidades de negócio → papel).</summary>
    [JsonPropertyName("memberships")]
    public IReadOnlyList<MembershipEntry> Memberships { get; set; } = [];

    /// <summary>Identificador do tenant ao qual esta sessão está escopada (PBT-01).</summary>
    [JsonPropertyName("tenant_id")]
    public Guid TenantId { get; set; }
}
