using NodaTime;

namespace Digest.Domain.ValueObjects;

/// <summary>
/// Objeto de valor imutável que encapsula a regra de elegibilidade do tenant para envio do digest.
/// Avalia se, dado o instante UTC do disparo, o horário local do tenant coincide com o
/// <c>digest_time</c> configurado e o dia local é um dia útil (Seg–Sex) (Req 2, PBT-01, DD-012).
/// </summary>
/// <remarks>
/// A conversão UTC→local usa NodaTime com base TZDB IANA embutida, neutralizando transições DST.
/// Sem dependência de <see cref="TimeZoneInfo"/> do sistema operacional.
/// </remarks>
public sealed class TenantEligibility
{
    private readonly ZonedDateTime _localDateTime;
    private readonly LocalTime _digestTime;
    private readonly bool _active;

    /// <summary>
    /// Constrói <see cref="TenantEligibility"/> a partir dos parâmetros de configuração do tenant.
    /// </summary>
    /// <param name="utcTriggerInstant">Instante UTC do disparo do Cloud Scheduler.</param>
    /// <param name="ianaTimezone">Identificador IANA do fuso do tenant (ex.: "America/Sao_Paulo").</param>
    /// <param name="digestTime">Horário local configurado para envio do digest (ex.: 07:00).</param>
    /// <param name="active">Indica se o tenant está ativo. Inativo nunca é elegível.</param>
    /// <param name="provider">Provedor de zonas de tempo TZDB (injetável para testabilidade).</param>
    /// <exception cref="ArgumentException">Quando <paramref name="ianaTimezone"/> não é reconhecido pelo TZDB.</exception>
    public TenantEligibility(
        Instant utcTriggerInstant,
        string ianaTimezone,
        LocalTime digestTime,
        bool active,
        IDateTimeZoneProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ianaTimezone);
        ArgumentNullException.ThrowIfNull(provider);

        var zone = provider[ianaTimezone]
            ?? throw new ArgumentException($"Fuso IANA não reconhecido: '{ianaTimezone}'.", nameof(ianaTimezone));

        _localDateTime = utcTriggerInstant.InZone(zone);
        _digestTime = digestTime;
        _active = active;
    }

    /// <summary>Data local do tenant derivada da conversão UTC→local.</summary>
    public LocalDate LocalDate => _localDateTime.Date;

    /// <summary>Dia da semana local do tenant (ISO).</summary>
    public IsoDayOfWeek LocalWeekday => _localDateTime.DayOfWeek;

    /// <summary>
    /// Retorna <c>true</c> se o tenant deve receber o disparo neste instante.
    /// Critério: tenant ativo, horário local (hora:minuto) == <c>digest_time</c> e dia útil (Seg–Sex).
    /// </summary>
    public bool IsEligible()
    {
        if (!_active)
            return false;

        var localTime = _localDateTime.TimeOfDay;
        var timeMatches = localTime.Hour == _digestTime.Hour
                          && localTime.Minute == _digestTime.Minute;
        var isWeekday = _localDateTime.DayOfWeek >= IsoDayOfWeek.Monday
                        && _localDateTime.DayOfWeek <= IsoDayOfWeek.Friday;

        return timeMatches && isWeekday;
    }

    /// <summary>
    /// Projeta a data local como <see cref="DigestDate"/> para uso em <c>email_digest_logs</c>.
    /// </summary>
    public DigestDate ToDigestDate() => new(_localDateTime.Date);
}
