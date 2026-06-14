using Digest.Domain.ValueObjects;
using FsCheck;
using FsCheck.Xunit;
using NodaTime;

namespace Digest.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários e PBT-01 para <see cref="TenantEligibility"/> e <see cref="DigestDate"/>.
/// Valida: conversão UTC→local neutra a DST; elegibilidade Seg–Sex; tenant inativo; PBT-01.
/// </summary>
public sealed class TenantEligibilityTests
{
    // ---------------------------------------------------------------
    // Testes nominais
    // ---------------------------------------------------------------

    [Fact]
    public void IsEligible_returns_true_when_local_time_matches_digest_time_on_weekday()
    {
        // America/Sao_Paulo UTC-3 (sem DST no inverno de 2026)
        // 10:00 UTC → 07:00 local; segunda-feira 2026-06-15 (Seg)
        var provider = DateTimeZoneProviders.Tzdb;
        var utcInstant = Instant.FromUtc(2026, 6, 15, 10, 0, 0);
        var digestTime = new LocalTime(7, 0);

        var sut = new TenantEligibility(
            utcTriggerInstant: utcInstant,
            ianaTimezone: "America/Sao_Paulo",
            digestTime: digestTime,
            active: true,
            provider: provider);

        sut.IsEligible().Should().BeTrue();
    }

    [Fact]
    public void IsEligible_returns_false_when_inactive()
    {
        var provider = DateTimeZoneProviders.Tzdb;
        var utcInstant = Instant.FromUtc(2026, 6, 15, 10, 0, 0);
        var digestTime = new LocalTime(7, 0);

        var sut = new TenantEligibility(
            utcTriggerInstant: utcInstant,
            ianaTimezone: "America/Sao_Paulo",
            digestTime: digestTime,
            active: false,
            provider: provider);

        sut.IsEligible().Should().BeFalse();
    }

    [Fact]
    public void IsEligible_returns_false_on_saturday()
    {
        // 2026-06-13 sábado 10:00 UTC → 07:00 local (America/Sao_Paulo, UTC-3)
        var provider = DateTimeZoneProviders.Tzdb;
        var utcInstant = Instant.FromUtc(2026, 6, 13, 10, 0, 0);
        var digestTime = new LocalTime(7, 0);

        var sut = new TenantEligibility(
            utcTriggerInstant: utcInstant,
            ianaTimezone: "America/Sao_Paulo",
            digestTime: digestTime,
            active: true,
            provider: provider);

        sut.IsEligible().Should().BeFalse();
    }

    [Fact]
    public void IsEligible_returns_false_on_sunday()
    {
        // 2026-06-14 domingo 10:00 UTC → 07:00 local (America/Sao_Paulo, UTC-3)
        var provider = DateTimeZoneProviders.Tzdb;
        var utcInstant = Instant.FromUtc(2026, 6, 14, 10, 0, 0);
        var digestTime = new LocalTime(7, 0);

        var sut = new TenantEligibility(
            utcTriggerInstant: utcInstant,
            ianaTimezone: "America/Sao_Paulo",
            digestTime: digestTime,
            active: true,
            provider: provider);

        sut.IsEligible().Should().BeFalse();
    }

    [Fact]
    public void IsEligible_returns_false_when_local_time_does_not_match_digest_time()
    {
        // 10:00 UTC → 07:00 local; digest_time = 08:00 → não elegível
        var provider = DateTimeZoneProviders.Tzdb;
        var utcInstant = Instant.FromUtc(2026, 6, 15, 10, 0, 0);
        var digestTime = new LocalTime(8, 0);

        var sut = new TenantEligibility(
            utcTriggerInstant: utcInstant,
            ianaTimezone: "America/Sao_Paulo",
            digestTime: digestTime,
            active: true,
            provider: provider);

        sut.IsEligible().Should().BeFalse();
    }

    [Fact]
    public void LocalDate_returns_tenant_local_date()
    {
        var provider = DateTimeZoneProviders.Tzdb;
        // 2026-06-16 01:00 UTC → 2026-06-15 22:00 local (America/New_York UTC-4 no verão)
        var utcInstant = Instant.FromUtc(2026, 6, 16, 1, 0, 0);
        var digestTime = new LocalTime(22, 0);

        var sut = new TenantEligibility(
            utcTriggerInstant: utcInstant,
            ianaTimezone: "America/New_York",
            digestTime: digestTime,
            active: true,
            provider: provider);

        sut.LocalDate.Should().Be(new LocalDate(2026, 6, 15));
    }

    [Fact]
    public void DigestDate_wraps_local_date_correctly()
    {
        var provider = DateTimeZoneProviders.Tzdb;
        var utcInstant = Instant.FromUtc(2026, 6, 15, 10, 0, 0);
        var digestTime = new LocalTime(7, 0);

        var sut = new TenantEligibility(
            utcTriggerInstant: utcInstant,
            ianaTimezone: "America/Sao_Paulo",
            digestTime: digestTime,
            active: true,
            provider: provider);

        var digestDate = sut.ToDigestDate();
        digestDate.Value.Should().Be(new LocalDate(2026, 6, 15));
    }

    // ---------------------------------------------------------------
    // PBT-01: para qualquer fuso IANA, digest_time e hora UTC
    // IsEligible() retorna verdadeiro SE E SOMENTE SE:
    //   - tenant está ativo
    //   - hora local == digest_time (minuto a minuto)
    //   - dia local é Seg–Sex (IsoDayOfWeek 1–5)
    // ---------------------------------------------------------------

    [Property(MaxTest = 200, Arbitrary = new[] { typeof(EligibilityArbitraries) })]
    public Property PBT01_eligibility_iff_active_and_time_matches_and_weekday(EligibilityInput input)
    {
        var provider = DateTimeZoneProviders.Tzdb;
        var zone = provider[input.IanaTimezone];
        var localDateTime = input.Instant.InZone(zone).LocalDateTime;
        var localTime = localDateTime.TimeOfDay;
        var isWeekday = localDateTime.DayOfWeek >= IsoDayOfWeek.Monday
                        && localDateTime.DayOfWeek <= IsoDayOfWeek.Friday;

        var sut = new TenantEligibility(
            utcTriggerInstant: input.Instant,
            ianaTimezone: input.IanaTimezone,
            digestTime: input.DigestTime,
            active: input.Active,
            provider: provider);

        var expected = input.Active
                       && localTime.Hour == input.DigestTime.Hour
                       && localTime.Minute == input.DigestTime.Minute
                       && isWeekday;

        return (sut.IsEligible() == expected).ToProperty();
    }
}

// ---------------------------------------------------------------------------
// Arbitraries para PBT-01 — geradores FsCheck
// ---------------------------------------------------------------------------

/// <summary>
/// Entrada gerada arbitrariamente para PBT-01.
/// </summary>
public record EligibilityInput(
    Instant Instant,
    string IanaTimezone,
    LocalTime DigestTime,
    bool Active);

/// <summary>
/// Geradores FsCheck para os tipos usados em PBT-01.
/// Fusos usados: subconjunto representativo com DST real (América, Europa, Ásia, Oceania).
/// </summary>
public static class EligibilityArbitraries
{
    // Fusos com transições DST reais e sem DST (UTC, fixos)
    private static readonly string[] KnownTimezones =
    [
        "America/Sao_Paulo",    // UTC-3/-2 (DST novembro–março)
        "America/New_York",     // UTC-5/-4 (DST março–novembro)
        "America/Chicago",      // UTC-6/-5
        "America/Los_Angeles",  // UTC-8/-7
        "Europe/London",        // UTC 0/+1
        "Europe/Paris",         // UTC+1/+2
        "Asia/Tokyo",           // UTC+9 (sem DST)
        "Australia/Sydney",     // UTC+10/+11
        "UTC",                  // sem DST
        "Pacific/Auckland",     // UTC+12/+13
    ];

    public static Arbitrary<EligibilityInput> ArbitraryEligibilityInput()
    {
        var genTimezone = Gen.Elements(KnownTimezones);

        // Instant em janela de 4 anos cobrindo transições DST
        var epochBase = Instant.FromUtc(2024, 1, 1, 0, 0, 0).ToUnixTimeSeconds();
        var epochEnd = Instant.FromUtc(2028, 1, 1, 0, 0, 0).ToUnixTimeSeconds();
        var genInstant = Gen.Choose((int)epochBase, (int)epochEnd)
            .Select(s => Instant.FromUnixTimeSeconds(s));

        // DigestTime: hora 0..23, minuto 0..59
        var genHour = Gen.Choose(0, 23);
        var genMinute = Gen.Choose(0, 59);
        var genDigestTime = genHour.SelectMany(h => genMinute, (h, m) => new LocalTime(h, m));

        var genActive = Arb.Default.Bool().Generator;

        var gen = from tz in genTimezone
                  from instant in genInstant
                  from dt in genDigestTime
                  from active in genActive
                  select new EligibilityInput(instant, tz, dt, active);

        return Arb.From(gen);
    }
}
