namespace Authentication.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que representa o slug único e imutável de um tenant Azim.
///
/// Invariantes (design.md § 4.3, Req 1):
///   - Valor não nulo, não vazio e não composto apenas de espaços em branco.
///   - Normalizado para lowercase com espaços removidos nas extremidades.
///   - Imutável após construção; igualdade por valor.
/// </summary>
public sealed class TenantSlug : IEquatable<TenantSlug>
{
    /// <summary>Valor normalizado do slug (lowercase, trimmed).</summary>
    public string Value { get; }

    private TenantSlug(string value) => Value = value;

    /// <summary>
    /// Cria um <see cref="TenantSlug"/> a partir de um valor bruto.
    ///
    /// Aplica normalização (lowercase + trim) e valida invariantes.
    /// </summary>
    /// <param name="value">Valor bruto do slug.</param>
    /// <returns>Instância imutável de <see cref="TenantSlug"/>.</returns>
    /// <exception cref="ArgumentException">Lançada quando <paramref name="value"/> é nulo, vazio ou whitespace.</exception>
    public static TenantSlug Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("TenantSlug não pode ser nulo, vazio ou composto apenas de espaços.", nameof(value));

        var normalized = value.Trim().ToLowerInvariant();
        return new TenantSlug(normalized);
    }

    /// <inheritdoc/>
    public bool Equals(TenantSlug? other) =>
        other is not null &&
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TenantSlug other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Value);

    /// <summary>Compara dois slugs por valor.</summary>
    public static bool operator ==(TenantSlug? left, TenantSlug? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Compara dois slugs por valor (desigualdade).</summary>
    public static bool operator !=(TenantSlug? left, TenantSlug? right) => !(left == right);

    /// <inheritdoc/>
    public override string ToString() => Value;
}
