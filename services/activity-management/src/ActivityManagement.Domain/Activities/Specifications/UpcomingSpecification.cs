namespace ActivityManagement.Domain.Activities.Specifications;

/// <summary>
/// Specification que determina se uma atividade vence após o dia corrente no fuso IANA do tenant.
/// "Próximas" são atividades não terminais com <c>dueAt</c> posterior ao dia corrente local.
/// A faixa é calculada no horário local do tenant (DD-008, Req 5.2).
/// Mapeia: design §4.6, Req 5.1, DD-008, TASK-05.
/// </summary>
public sealed class UpcomingSpecification
{
    private readonly DateTimeOffset _startOfTomorrowUtc;

    /// <summary>
    /// Inicializa a specification com o instante corrente e o fuso horário do tenant.
    /// </summary>
    /// <param name="now">Instante corrente (UTC).</param>
    /// <param name="tenantTimeZone">Fuso IANA do tenant para cálculo do início do próximo dia local.</param>
    public UpcomingSpecification(DateTimeOffset now, TimeZoneInfo tenantTimeZone)
    {
        var localNow      = TimeZoneInfo.ConvertTime(now, tenantTimeZone);
        var localToday    = localNow.Date;
        var tomorrowLocal = localToday.AddDays(1);

        _startOfTomorrowUtc = new DateTimeOffset(
            tomorrowLocal,
            tenantTimeZone.GetUtcOffset(tomorrowLocal))
            .ToUniversalTime();
    }

    /// <summary>
    /// Avalia se a atividade está na faixa "próximas".
    /// </summary>
    /// <param name="activity">Atividade a avaliar.</param>
    /// <returns><c>true</c> quando não terminal e <c>dueAt</c> ≥ início do dia seguinte no fuso do tenant.</returns>
    public bool IsSatisfiedBy(Activity activity)
    {
        if (activity.Status.IsTerminal)
            return false;

        return activity.DueAt.Value.ToUniversalTime() >= _startOfTomorrowUtc;
    }
}
