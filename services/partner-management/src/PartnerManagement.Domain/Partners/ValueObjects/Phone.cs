namespace PartnerManagement.Domain.Partners.ValueObjects;

/// <summary>
/// Objeto de valor que representa um número de telefone composto apenas de dígitos significativos.
/// Opcional no <see cref="PartnerContact"/>. Tratado como possível PII (DD-008, RNF 4).
/// Mapeia: Req 7.1, design §4.3.
/// </summary>
public sealed class Phone : IEquatable<Phone>
{
    /// <summary>Dígitos significativos do número de telefone.</summary>
    public string Value { get; }

    private Phone(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Cria um <see cref="Phone"/> válido a partir de uma string de dígitos.
    /// </summary>
    /// <param name="value">Número de telefone (somente dígitos).</param>
    /// <returns>Instância válida de <see cref="Phone"/>.</returns>
    /// <exception cref="ArgumentException">Lançada quando o valor é nulo, vazio ou somente espaços.</exception>
    public static Phone Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Número de telefone não pode ser vazio.", nameof(value));
        }

        return new Phone(value.Trim());
    }

    /// <inheritdoc/>
    public bool Equals(Phone? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Phone other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Value);

    /// <summary>Retorna representação mascarada — não expõe PII (RNF 4, DD-008).</summary>
    public override string ToString() => "[Phone]";

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(Phone? left, Phone? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(Phone? left, Phone? right) => !(left == right);
}
