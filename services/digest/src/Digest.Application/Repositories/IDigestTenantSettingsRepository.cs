namespace Digest.Application.Repositories;

/// <summary>
/// Repositório de configuração de digest por tenant.
/// Permite consultar o TTL personalizado do action token para um tenant específico.
/// Quando o tenant não possuir configuração própria, retorna <c>null</c> —
/// o chamador deve aplicar o default global (design VAL-ACT-02).
/// </summary>
/// <remarks>
/// Implementação concreta em <c>Digest.Infrastructure</c>.
/// A tabela <c>digest_tenant_settings</c> é protegida por RLS (ADR-0001).
/// </remarks>
public interface IDigestTenantSettingsRepository
{
    /// <summary>
    /// Retorna o TTL em horas configurado para o tenant, ou <c>null</c> quando não configurado.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// Horas configuradas (inteiro positivo) quando o tenant possui setting próprio;
    /// <c>null</c> quando não há configuração — o chamador usa o default global de 48h.
    /// </returns>
    Task<int?> GetActionTokenTtlHoursAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
