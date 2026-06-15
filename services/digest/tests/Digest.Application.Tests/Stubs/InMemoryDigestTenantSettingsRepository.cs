using Digest.Application.Repositories;

namespace Digest.Application.Tests.Stubs;

/// <summary>
/// Stub in-memory de <see cref="IDigestTenantSettingsRepository"/> para testes de Application.
/// Permite configurar o TTL por tenant ou simular ausência de configuração (fallback para default).
/// </summary>
public sealed class InMemoryDigestTenantSettingsRepository : IDigestTenantSettingsRepository
{
    private readonly Dictionary<Guid, int> _settings = new();

    /// <summary>
    /// Configura o TTL em horas para um tenant específico.
    /// </summary>
    public void SetTtlHours(Guid tenantId, int ttlHours) => _settings[tenantId] = ttlHours;

    /// <summary>
    /// Remove a configuração do tenant (simula ausência de setting — usa default).
    /// </summary>
    public void ClearTenant(Guid tenantId) => _settings.Remove(tenantId);

    /// <inheritdoc/>
    public Task<int?> GetActionTokenTtlHoursAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        int? result = _settings.TryGetValue(tenantId, out var hours) ? hours : null;
        return Task.FromResult(result);
    }
}
