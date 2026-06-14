namespace ActivityManagement.Application.Ports;

/// <summary>
/// Port de relógio no fuso horário IANA configurado no tenant (DD-008).
/// Utilizado pelas queries de visão (<c>GetMyDayQuery</c>, <c>GetMyWeekQuery</c>)
/// e pelas specifications <c>TodaySpecification</c> e <c>UpcomingSpecification</c>
/// para calcular faixas de data no horário local do tenant, não em UTC.
/// Implementado em Infrastructure.
/// Mapeia: Req 5.2, DD-008, design §4.6/§5.2.
/// </summary>
public interface ITenantClock
{
    /// <summary>
    /// Retorna o instante corrente no fuso IANA do tenant.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant cujo fuso será usado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    DateTimeOffset GetNowForTenant(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna o início do dia corrente (00:00:00) no fuso IANA do tenant.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant cujo fuso será usado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    DateTimeOffset GetStartOfDayForTenant(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna o início da semana corrente (segunda-feira 00:00:00) no fuso IANA do tenant.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant cujo fuso será usado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    DateTimeOffset GetStartOfWeekForTenant(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna o fim da semana corrente (domingo 23:59:59) no fuso IANA do tenant.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant cujo fuso será usado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    DateTimeOffset GetEndOfWeekForTenant(Guid tenantId, CancellationToken cancellationToken = default);
}
