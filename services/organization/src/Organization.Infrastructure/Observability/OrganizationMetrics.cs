using System.Diagnostics.Metrics;

namespace Organization.Infrastructure.Observability;

/// <summary>
/// Métricas do módulo Organization usando <see cref="System.Diagnostics.Metrics"/>.
/// Todas as métricas seguem snake_case conforme design §11.
/// Nenhuma métrica contém PII (e-mail, display_name).
/// </summary>
public sealed class OrganizationMetrics : IDisposable
{
    /// <summary>Nome do meter do módulo.</summary>
    public const string MeterName = "organization";

    private readonly Meter _meter;

    /// <summary>Contador de usuários convidados.</summary>
    public readonly Counter<long> UsersInvitedTotal;

    /// <summary>Contador de usuários desativados.</summary>
    public readonly Counter<long> UsersDeactivatedTotal;

    /// <summary>Contador de Business Units criadas.</summary>
    public readonly Counter<long> BuCreatedTotal;

    /// <summary>Contador de hits no cache de memberships.</summary>
    public readonly Counter<long> MembershipCacheHitTotal;

    /// <summary>Contador de misses no cache de memberships.</summary>
    public readonly Counter<long> MembershipCacheMissTotal;

    /// <summary>Contador de violações RLS detectadas (severidade crítica).</summary>
    public readonly Counter<long> TenantRlsViolationCount;

    /// <summary>Inicializa o meter e os instrumentos de métricas.</summary>
    public OrganizationMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        UsersInvitedTotal = _meter.CreateCounter<long>(
            "users_invited_total",
            description: "Total de convites de usuário emitidos com sucesso.");

        UsersDeactivatedTotal = _meter.CreateCounter<long>(
            "users_deactivated_total",
            description: "Total de usuários desativados (soft-delete).");

        BuCreatedTotal = _meter.CreateCounter<long>(
            "bu_created_total",
            description: "Total de Business Units criadas.");

        MembershipCacheHitTotal = _meter.CreateCounter<long>(
            "membership_cache_hit_total",
            description: "Acertos no cache Redis de contexto RBAC.");

        MembershipCacheMissTotal = _meter.CreateCounter<long>(
            "membership_cache_miss_total",
            description: "Falhas no cache Redis de contexto RBAC (miss ou degradação).");

        TenantRlsViolationCount = _meter.CreateCounter<long>(
            "tenant_rls_violation_count",
            description: "Violações de isolamento de tenant detectadas. Alerta imediato se > 0.");
    }

    /// <inheritdoc/>
    public void Dispose() => _meter.Dispose();
}
