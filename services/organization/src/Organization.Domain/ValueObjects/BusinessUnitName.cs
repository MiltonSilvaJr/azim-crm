namespace Organization.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que representa o nome de uma Business Unit.
/// Imutável, com igualdade por valor normalizado (trim + case-insensitive).
/// Comprimento: 1 a 120 caracteres após trim.
/// </summary>
public sealed class BusinessUnitName : IEquatable<BusinessUnitName>
{
    /// <summary>Valor normalizado (trimmed) do nome.</summary>
    public string Value { get; }

    private BusinessUnitName(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Cria um <see cref="BusinessUnitName"/> validado.
    /// </summary>
    /// <param name="value">Nome da BU (será trimado).</param>
    /// <exception cref="ArgumentException">Quando vazio, nulo ou excede 120 caracteres após trim.</exception>
    public static BusinessUnitName Create(string value)
    {
        if (value is null)
            throw new ArgumentException("O nome da Business Unit não pode ser nulo.", nameof(value));

        var trimmed = value.Trim();

        if (trimmed.Length == 0)
            throw new ArgumentException("O nome da Business Unit não pode ser vazio.", nameof(value));

        if (trimmed.Length > 120)
            throw new ArgumentException("O nome da Business Unit não pode exceder 120 caracteres.", nameof(value));

        return new BusinessUnitName(trimmed);
    }

    /// <summary>
    /// Retorna a chave de comparação para unicidade (normalizada para minúsculas).
    /// Usada para comparação case-insensitive conforme Req 1.3.
    /// </summary>
    public string NormalizedKey => Value.ToUpperInvariant();

    /// <inheritdoc/>
    public bool Equals(BusinessUnitName? other)
    {
        if (other is null) return false;
        return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is BusinessUnitName other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.ToUpperInvariant().GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Operador de igualdade.</summary>
    public static bool operator ==(BusinessUnitName? left, BusinessUnitName? right)
        => left?.Equals(right) ?? right is null;

    /// <summary>Operador de desigualdade.</summary>
    public static bool operator !=(BusinessUnitName? left, BusinessUnitName? right)
        => !(left == right);
}
