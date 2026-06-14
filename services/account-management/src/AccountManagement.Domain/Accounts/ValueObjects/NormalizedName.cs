namespace AccountManagement.Domain.Accounts.ValueObjects;

/// <summary>
/// Objeto de valor que representa a forma normalizada do nome de uma conta,
/// usada como base para deduplificação e busca por nome.
///
/// Invariantes:
/// - Resultado determinístico do algoritmo de <see cref="Services.NameNormalizer"/>.
/// - Imutável; igualdade por valor.
/// - Base de dedupe e busca indexada (design §4.3, RNF 7.1).
///
/// Mapeia: design §4.3, Req 1.1, Req 3.2, PBT-01, PBT-02, DD-005, DD-006.
/// </summary>
public sealed class NormalizedName : IEquatable<NormalizedName>
{
    /// <summary>Valor normalizado.</summary>
    public string Value { get; }

    private NormalizedName(string value) => Value = value;

    /// <summary>
    /// Cria um <see cref="NormalizedName"/> a partir de um valor já normalizado.
    /// Use <see cref="Services.NameNormalizer"/> para normalizar antes de criar.
    /// </summary>
    /// <param name="normalizedValue">Valor já normalizado.</param>
    /// <returns>Instância de <see cref="NormalizedName"/>.</returns>
    public static NormalizedName Create(string normalizedValue) =>
        new(normalizedValue ?? string.Empty);

    /// <inheritdoc />
    public bool Equals(NormalizedName? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is NormalizedName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Compara dois <see cref="NormalizedName"/> por valor.</summary>
    public static bool operator ==(NormalizedName? left, NormalizedName? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Compara dois <see cref="NormalizedName"/> por desigualdade de valor.</summary>
    public static bool operator !=(NormalizedName? left, NormalizedName? right) => !(left == right);
}
