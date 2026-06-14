namespace ActivityManagement.Domain.Activities.Specifications;

/// <summary>
/// Specification que determina se uma atividade vence no dia corrente no fuso IANA do tenant.
/// A faixa "hoje" é calculada no horário local do tenant (DD-008, Req 5.2).
/// Atividades terminais são excluídas automaticamente.
/// Mapeia: design §4.6, Req 5.1, Req 11.4, DD-008, TASK-05.
/// </summary>
public sealed class TodaySpecification
{
    private readonly DateTimeOffset _startOfTodayUtc;
    private readonly DateTimeOffset _startOfTomorrowUtc;

    /// <summary>
    /// Inicializa a specification com o instante corrente e o fuso horário do tenant.
    /// </summary>
    /// <param name="now">Instante corrente (UTC).</param>
    /// <param name="tenantTimeZone">Fuso IANA do tenant para cálculo do "hoje" local.</param>
    public TodaySpecification(DateTimeOffset now, TimeZoneInfo tenantTimeZone)
    {
        // Converte o instante atual para o fuso do tenant para obter a data local
        var localNow   = TimeZoneInfo.ConvertTime(now, tenantTimeZone);
        var localToday = localNow.Date;

        // Converte os limites do dia local de volta para UTC
        var startLocal   = new DateTimeOffset(localToday, tenantTimeZone.GetUtcOffset(localToday));
        var tomorrowLocal = new DateTimeOffset(localToday.AddDays(1), tenantTimeZone.GetUtcOffset(localToday.AddDays(1)));

        _startOfTodayUtc    = startLocal.ToUniversalTime();
        _startOfTomorrowUtc = tomorrowLocal.ToUniversalTime();
    }

    /// <summary>
    /// Avalia se a atividade vence hoje no fuso do tenant.
    /// </summary>
    /// <param name="activity">Atividade a avaliar.</param>
    /// <returns><c>true</c> quando a atividade não é terminal e vence no dia corrente do tenant.</returns>
    public bool IsSatisfiedBy(Activity activity)
    {
        if (activity.Status.IsTerminal)
            return false;

        var dueAtUtc = activity.DueAt.Value.ToUniversalTime();
        return dueAtUtc >= _startOfTodayUtc && dueAtUtc < _startOfTomorrowUtc;
    }
}
