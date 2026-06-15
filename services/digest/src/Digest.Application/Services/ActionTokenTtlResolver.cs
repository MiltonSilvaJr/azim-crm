using Digest.Application.Options;
using Digest.Application.Repositories;

namespace Digest.Application.Services;

/// <summary>
/// Serviço de Application responsável por resolver o TTL efetivo do action token para um tenant.
/// Encapsula a lógica de fallback: setting do tenant quando presente; default global quando ausente.
/// Decisão de produto VAL-ACT-02 (2026-06-15): TTL configurável por tenant com default de 48h.
/// </summary>
public sealed class ActionTokenTtlResolver
{
    private readonly IDigestTenantSettingsRepository _settingsRepository;
    private readonly DigestOptions _options;

    /// <summary>
    /// Constrói o resolver com o repositório de settings e as opções globais.
    /// </summary>
    public ActionTokenTtlResolver(
        IDigestTenantSettingsRepository settingsRepository,
        DigestOptions options)
    {
        _settingsRepository = settingsRepository;
        _options = options;
    }

    /// <summary>
    /// Retorna o <see cref="TimeSpan"/> de TTL efetivo para o tenant.
    /// Usa o setting do tenant quando presente; caso contrário, aplica o default global.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>TTL positivo como <see cref="TimeSpan"/>.</returns>
    public async Task<TimeSpan> ResolveAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantTtlHours = await _settingsRepository.GetActionTokenTtlHoursAsync(tenantId, cancellationToken);

        var effectiveHours = tenantTtlHours ?? _options.DefaultActionTokenTtlHours;

        return TimeSpan.FromHours(effectiveHours);
    }
}
