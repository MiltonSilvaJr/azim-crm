namespace TenantAdministration.Infrastructure.Persistence;

/// <summary>
/// Status possíveis de uma requisição de provisionamento (design.md §7.3).
/// </summary>
public enum ProvisioningRequestStatus
{
    InProgress,
    Succeeded,
    Failed
}

/// <summary>
/// Registro de idempotência para <c>ProvisionTenantCommand</c>.
/// Armazenado em <c>tenant_provisioning_requests</c> (design.md §7.3, §6.5).
/// </summary>
public sealed class TenantProvisioningRequest
{
    /// <summary>Chave de idempotência (PK).</summary>
    public string IdempotencyKey { get; init; } = default!;

    /// <summary>Slug do tenant sendo provisionado.</summary>
    public string Slug { get; init; } = default!;

    /// <summary>ID do tenant criado (preenchido após sucesso).</summary>
    public Guid? ResultTenantId { get; set; }

    /// <summary>ID do tenant no Identity Platform (preenchido após sucesso).</summary>
    public string? ResultIdentityTenantId { get; set; }

    /// <summary>Status da requisição.</summary>
    public ProvisioningRequestStatus Status { get; set; }

    /// <summary>Data de criação.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
