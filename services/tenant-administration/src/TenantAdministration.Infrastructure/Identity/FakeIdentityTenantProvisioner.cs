namespace TenantAdministration.Infrastructure.Identity;

/// <summary>
/// Implementação fake de <see cref="IIdentityTenantProvisioner"/> para testes.
/// Permite injetar falhas por posição de chamada para testar PBT-07 (atomicidade).
/// </summary>
public sealed class FakeIdentityTenantProvisioner : IIdentityTenantProvisioner
{
    private readonly HashSet<string> _created = [];
    private readonly HashSet<string> _deleted = [];

    /// <summary>
    /// Quando verdadeiro, <see cref="CreateTenantAsync"/> lança exceção.
    /// Usado para simular falha antes da persistência no banco (PBT-07).
    /// </summary>
    public bool ShouldFailOnCreate { get; set; }

    /// <summary>
    /// Quando verdadeiro, <see cref="DeleteTenantAsync"/> lança exceção.
    /// </summary>
    public bool ShouldFailOnDelete { get; set; }

    /// <summary>Slugs para os quais o tenant foi criado com sucesso.</summary>
    public IReadOnlySet<string> Created => _created;

    /// <summary>IDs de tenants deletados na compensação.</summary>
    public IReadOnlySet<string> Deleted => _deleted;

    /// <inheritdoc/>
    public Task<IdentityProvisioningResult> CreateTenantAsync(
        string slug,
        string adminEmail,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        if (ShouldFailOnCreate)
            throw new InvalidOperationException("TA-ERR-009: Falha simulada no Identity Platform.");

        _created.Add(slug);
        return Task.FromResult(new IdentityProvisioningResult($"fake-idp-{slug}"));
    }

    /// <inheritdoc/>
    public Task DeleteTenantAsync(string identityTenantId, CancellationToken ct = default)
    {
        if (ShouldFailOnDelete)
            throw new InvalidOperationException("Falha simulada no delete do Identity Platform.");

        _deleted.Add(identityTenantId);
        return Task.CompletedTask;
    }

    /// <summary>Reseta o estado do fake para reutilização entre testes.</summary>
    public void Reset()
    {
        _created.Clear();
        _deleted.Clear();
        ShouldFailOnCreate = false;
        ShouldFailOnDelete = false;
    }
}
