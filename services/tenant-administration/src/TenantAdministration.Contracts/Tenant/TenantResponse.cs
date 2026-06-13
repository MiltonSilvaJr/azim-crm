namespace TenantAdministration.Contracts.Tenant;

/// <summary>
/// Payload de response para leitura do tenant corrente.
/// Não expõe identityTenantId nem adminEmail (Req 9.2).
/// design.md §8.2 GET /api/v1/tenant.
/// </summary>
public sealed record TenantResponse(
    Guid TenantId,
    string Slug,
    string DisplayName,
    string Timezone,
    string DigestTime,
    string Status);
