using NodaTime;

namespace Digest.Domain.ValueObjects;

/// <summary>
/// Data local do tenant no fuso IANA configurado, derivada da conversão UTC→local.
/// Representa o <c>digest_date</c> registrado em <c>email_digest_logs</c> (Req 8.3).
/// Imutável; igualdade por valor.
/// </summary>
public sealed record DigestDate
{
    /// <summary>Data local (sem componente de horário) no fuso do tenant.</summary>
    public LocalDate Value { get; }

    /// <summary>
    /// Constrói um <see cref="DigestDate"/> a partir de uma data local NodaTime.
    /// </summary>
    /// <param name="localDate">Data local derivada de conversão UTC→local via TZDB.</param>
    public DigestDate(LocalDate localDate)
    {
        Value = localDate;
    }

    /// <summary>Retorna <c>true</c> se a data cai em segunda-feira (ISO: 1).</summary>
    public bool IsMonday() => Value.DayOfWeek == IsoDayOfWeek.Monday;

    /// <summary>Representação ISO 8601 da data local (yyyy-MM-dd).</summary>
    public override string ToString() => Value.ToString("yyyy-MM-dd", null);
}
