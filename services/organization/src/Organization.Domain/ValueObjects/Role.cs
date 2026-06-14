namespace Organization.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que representa o papel (role) de um usuário em uma Business Unit.
/// Valores canônicos: <c>TAdmin</c>, <c>GestorBU</c>, <c>Vendedor</c>, <c>Viewer</c>.
/// <c>PlatOp</c> é papel de plataforma e não é um membership válido.
/// </summary>
public sealed class Role : IEquatable<Role>
{
    private static readonly HashSet<string> ValidValues = new(StringComparer.Ordinal)
    {
        "TAdmin", "GestorBU", "Vendedor", "Viewer"
    };

    /// <summary>Valor canônico do papel.</summary>
    public string Value { get; }

    private Role(string value)
    {
        Value = value;
    }

    /// <summary>Papel de Administrador do Tenant.</summary>
    public static Role TAdmin { get; } = new("TAdmin");

    /// <summary>Papel de Gestor de Business Unit.</summary>
    public static Role GestorBU { get; } = new("GestorBU");

    /// <summary>Papel de Vendedor.</summary>
    public static Role Vendedor { get; } = new("Vendedor");

    /// <summary>Papel de Visualizador.</summary>
    public static Role Viewer { get; } = new("Viewer");

    /// <summary>
    /// Cria um <see cref="Role"/> validado a partir de uma string.
    /// </summary>
    /// <param name="value">Valor canônico do papel.</param>
    /// <exception cref="ArgumentException">Quando o valor não é canônico ou é nulo/vazio.</exception>
    public static Role Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("O papel (role) não pode ser nulo ou vazio.", nameof(value));

        if (!ValidValues.Contains(value))
            throw new ArgumentException(
                $"O papel '{value}' não é válido. Valores aceitos: TAdmin, GestorBU, Vendedor, Viewer.",
                nameof(value));

        return new Role(value);
    }

    /// <summary>Retorna todos os valores canônicos de papel.</summary>
    public static IReadOnlySet<string> AllValues => ValidValues;

    /// <inheritdoc/>
    public bool Equals(Role? other)
    {
        if (other is null) return false;
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Role other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Operador de igualdade.</summary>
    public static bool operator ==(Role? left, Role? right)
        => left?.Equals(right) ?? right is null;

    /// <summary>Operador de desigualdade.</summary>
    public static bool operator !=(Role? left, Role? right)
        => !(left == right);
}
