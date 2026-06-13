namespace TenantAdministration.Contracts.Tenant;

/// <summary>
/// Payload de request para atualizar fuso horário e horário do digest.
/// Não aceita o campo slug (TA-ERR-011 — Req 2.3).
/// design.md §8.2 PATCH /api/v1/tenant.
/// </summary>
public sealed record UpdateDigestConfigRequest(
    string Timezone,
    string DigestTime,
    string? Slug = null);
