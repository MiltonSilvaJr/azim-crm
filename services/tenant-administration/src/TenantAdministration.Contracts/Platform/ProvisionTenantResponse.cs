namespace TenantAdministration.Contracts.Platform;

/// <summary>
/// Payload de response do provisionamento bem-sucedido de um tenant.
/// Não expõe adminEmail nem identityTenantId.
/// design.md §8.1 — 201 Created.
/// </summary>
public sealed record ProvisionTenantResponse(
    Guid TenantId,
    string Slug,
    string Status);
