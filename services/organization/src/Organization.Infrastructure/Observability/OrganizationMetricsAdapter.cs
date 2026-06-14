using Organization.Application.Ports;

namespace Organization.Infrastructure.Observability;

/// <summary>
/// Adapter que implementa <see cref="IOrganizationMetrics"/> delegando para <see cref="OrganizationMetrics"/>.
/// Registrado como Singleton no contêiner DI (metrics são thread-safe por design).
/// </summary>
public sealed class OrganizationMetricsAdapter : IOrganizationMetrics
{
    private readonly OrganizationMetrics _metrics;

    /// <summary>Inicializa o adapter com os instrumentos de métricas.</summary>
    public OrganizationMetricsAdapter(OrganizationMetrics metrics)
    {
        _metrics = metrics;
    }

    /// <inheritdoc/>
    public void IncrementUsersInvited() => _metrics.UsersInvitedTotal.Add(1);

    /// <inheritdoc/>
    public void IncrementUsersDeactivated() => _metrics.UsersDeactivatedTotal.Add(1);

    /// <inheritdoc/>
    public void IncrementBuCreated() => _metrics.BuCreatedTotal.Add(1);

    /// <inheritdoc/>
    public void IncrementMembershipCacheHit() => _metrics.MembershipCacheHitTotal.Add(1);

    /// <inheritdoc/>
    public void IncrementMembershipCacheMiss() => _metrics.MembershipCacheMissTotal.Add(1);

    /// <inheritdoc/>
    public void IncrementTenantRlsViolation() => _metrics.TenantRlsViolationCount.Add(1);
}
