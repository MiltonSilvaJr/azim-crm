namespace TenantAdministration.Contracts.Platform;

/// <summary>
/// Payload de request para provisionar um novo tenant.
/// design.md §8.1 POST /api/v1/platform/tenants.
/// </summary>
public sealed record ProvisionTenantRequest(
    string Slug,
    string SlugConfirmation,
    string DisplayName,
    string Timezone,
    string DigestTime,
    string AdminEmail);
