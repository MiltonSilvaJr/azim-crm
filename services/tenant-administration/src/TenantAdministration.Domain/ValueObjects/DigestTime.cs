using System.Text.RegularExpressions;
using TenantAdministration.Domain.Common;

namespace TenantAdministration.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que representa o horário local do digest no formato <c>HH:mm</c>.
/// O fuso horário é determinado pelo <see cref="TimezoneIana"/> do tenant, não por este objeto.
/// </summary>
public sealed class DigestTime : IEquatable<DigestTime>
{
    private const string DefaultValue = "07:00";

    private static readonly Regex Pattern =
        new(@"^(0\d|1\d|2[0-3]):[0-5]\d$", RegexOptions.Compiled);

    /// <summary>Valor padrão conforme Req 3.3: <c>07:00</c>.</summary>
    public static readonly DigestTime Default = new(DefaultValue);

    /// <summary>Horário no formato HH:mm.</summary>
    public string Value { get; }

    private DigestTime(string value) => Value = value;

    /// <summary>
    /// Cria um <see cref="DigestTime"/> validando o formato <c>HH:mm</c>.
    /// </summary>
    /// <param name="time">Horário no formato <c>HH:mm</c>.</param>
    /// <returns>Sucesso com o objeto de valor ou falha com código de erro.</returns>
    public static Result<DigestTime> Create(string time)
    {
        if (string.IsNullOrWhiteSpace(time))
            return Fail();

        if (!Pattern.IsMatch(time.Trim()))
            return Fail();

        return Result<DigestTime>.Success(new DigestTime(time.Trim()));
    }

    private static Result<DigestTime> Fail() =>
        Result<DigestTime>.Failure("TA-ERR-DIGEST", "Horário do digest inválido; use o formato HH:mm (ex.: 07:00).");

    /// <inheritdoc/>
    public bool Equals(DigestTime? other) => other is not null && Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DigestTime other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Igualdade estrutural por valor.</summary>
    public static bool operator ==(DigestTime? left, DigestTime? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Desigualdade estrutural por valor.</summary>
    public static bool operator !=(DigestTime? left, DigestTime? right) => !(left == right);
}
