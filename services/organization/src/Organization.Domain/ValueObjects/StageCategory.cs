namespace Organization.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que representa a categoria de um estágio do pipeline de vendas.
/// Valores canônicos: <c>open</c>, <c>won</c>, <c>lost</c>.
/// Categorias terminais: <c>won</c> e <c>lost</c>.
/// </summary>
public sealed class StageCategory : IEquatable<StageCategory>
{
    private static readonly HashSet<string> ValidValues = new(StringComparer.Ordinal)
    {
        "open", "won", "lost"
    };

    /// <summary>Valor canônico da categoria.</summary>
    public string Value { get; }

    private StageCategory(string value)
    {
        Value = value;
    }

    /// <summary>Categoria aberta (não terminal).</summary>
    public static StageCategory Open { get; } = new("open");

    /// <summary>Categoria ganha (terminal).</summary>
    public static StageCategory Won { get; } = new("won");

    /// <summary>Categoria perdida (terminal).</summary>
    public static StageCategory Lost { get; } = new("lost");

    /// <summary>
    /// Indica se a categoria é terminal (<c>won</c> ou <c>lost</c>).
    /// Utilizado pela <c>TerminalStagesPolicy</c>.
    /// </summary>
    public bool IsTerminal => !string.Equals(Value, "open", StringComparison.Ordinal);

    /// <summary>
    /// Cria um <see cref="StageCategory"/> validado a partir de uma string.
    /// A comparação é case-sensitive (Req 9.1).
    /// </summary>
    /// <param name="value">Valor canônico da categoria.</param>
    /// <exception cref="ArgumentException">Quando o valor não é canônico ou é nulo/vazio.</exception>
    public static StageCategory Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A categoria do estágio não pode ser nula ou vazia.", nameof(value));

        if (!ValidValues.Contains(value))
            throw new ArgumentException(
                $"A categoria '{value}' não é válida. Valores aceitos: open, won, lost.",
                nameof(value));

        return new StageCategory(value);
    }

    /// <inheritdoc/>
    public bool Equals(StageCategory? other)
    {
        if (other is null) return false;
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is StageCategory other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Operador de igualdade.</summary>
    public static bool operator ==(StageCategory? left, StageCategory? right)
        => left?.Equals(right) ?? right is null;

    /// <summary>Operador de desigualdade.</summary>
    public static bool operator !=(StageCategory? left, StageCategory? right)
        => !(left == right);
}
