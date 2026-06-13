using TenantAdministration.Domain.Common;
using TenantAdministration.Domain.Errors;

namespace TenantAdministration.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que representa um identificador de fuso horário IANA válido.
/// Validação usa a base IANA do runtime .NET via <see cref="TimeZoneInfo"/>.
/// </summary>
public sealed class TimezoneIana : IEquatable<TimezoneIana>
{
    private const string DefaultValue = "America/Sao_Paulo";

    /// <summary>Valor padrão conforme Req 3.2: <c>America/Sao_Paulo</c>.</summary>
    public static readonly TimezoneIana Default = new(DefaultValue);

    /// <summary>Identificador IANA validado.</summary>
    public string Value { get; }

    private TimezoneIana(string value) => Value = value;

    /// <summary>
    /// Cria um <see cref="TimezoneIana"/> validando o identificador contra a base IANA do runtime.
    /// </summary>
    /// <param name="identifier">Identificador IANA (ex.: <c>America/Sao_Paulo</c>).</param>
    /// <returns>Sucesso com o objeto de valor ou falha com TA-ERR-006.</returns>
    public static Result<TimezoneIana> Create(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return Fail();

        try
        {
            // Valida contra a base IANA do runtime
            _ = TimeZoneInfo.FindSystemTimeZoneById(identifier);
            return Result<TimezoneIana>.Success(new TimezoneIana(identifier));
        }
        catch (TimeZoneNotFoundException)
        {
            return Fail();
        }
        catch (InvalidTimeZoneException)
        {
            return Fail();
        }
        catch (System.Security.SecurityException)
        {
            // Ocorre no macOS quando o identificador corresponde a um diretório
            // no tzdata (ex.: "America" sem região), não a um fuso válido
            return Fail();
        }
        catch (UnauthorizedAccessException)
        {
            return Fail();
        }
    }

    private static Result<TimezoneIana> Fail()
    {
        var (code, message) = DomainErrors.TimezoneInvalidIana;
        return Result<TimezoneIana>.Failure(code, message);
    }

    /// <inheritdoc/>
    public bool Equals(TimezoneIana? other) => other is not null && Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TimezoneIana other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Igualdade estrutural por valor.</summary>
    public static bool operator ==(TimezoneIana? left, TimezoneIana? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Desigualdade estrutural por valor.</summary>
    public static bool operator !=(TimezoneIana? left, TimezoneIana? right) => !(left == right);
}
