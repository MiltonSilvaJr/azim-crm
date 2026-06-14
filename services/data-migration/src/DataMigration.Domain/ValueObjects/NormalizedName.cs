namespace DataMigration.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que representa um nome de conta normalizado para dedupe.
///
/// Normalização (design §4.3, Req 7, RN-014):
///   - Trim de espaços iniciais e finais.
///   - Conversão para minúsculas (comparação insensível a case).
///
/// Imutável; igualdade por valor normalizado.
///
/// Rastreia: design §4.3, Req 7, PBT-04, TASK-07.
/// </summary>
public sealed class NormalizedName : IEquatable<NormalizedName>
{
    /// <summary>Valor normalizado (trim + lowercase).</summary>
    public string Value { get; }

    private NormalizedName(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Cria um <see cref="NormalizedName"/> a partir de um nome bruto.
    /// Aplica trim e conversão para lowercase.
    /// </summary>
    /// <exception cref="ArgumentException">Quando o nome é nulo ou vazio após trim.</exception>
    public static NormalizedName From(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException(
                "O nome da conta não pode ser nulo ou vazio.", nameof(input));
        }

        return new NormalizedName(input.Trim().ToLowerInvariant());
    }

    /// <inheritdoc />
    public bool Equals(NormalizedName? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as NormalizedName);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(NormalizedName? left, NormalizedName? right) =>
        Equals(left, right);

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(NormalizedName? left, NormalizedName? right) =>
        !Equals(left, right);
}
