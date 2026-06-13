namespace TenantAdministration.Contracts.Platform;

/// <summary>
/// Payload de response das operações de estado do tenant (suspend/reactivate).
/// design.md §8.1.
/// </summary>
public sealed record TenantStateResponse(
    Guid TenantId,
    string Status);
