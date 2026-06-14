namespace Organization.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que representa a probabilidade de fechamento de um estágio.
/// Intervalo válido: 0 a 100 (inteiro, sem casas decimais).
/// Não representa valor monetário; representa percentual de conversão.
/// </summary>
public sealed class Probability : IEquatable<Probability>
{
    /// <summary>Valor inteiro da probabilidade (0..100).</summary>
    public int Value { get; }

    private Probability(int value)
    {
        Value = value;
    }

    /// <summary>Probabilidade zero (tipicamente: estágio perdido).</summary>
    public static Probability Zero { get; } = new(0);

    /// <summary>Probabilidade cem (tipicamente: estágio ganho).</summary>
    public static Probability Hundred { get; } = new(100);

    /// <summary>
    /// Cria um <see cref="Probability"/> validado.
    /// </summary>
    /// <param name="value">Percentual entre 0 e 100.</param>
    /// <exception cref="ArgumentException">Quando o valor está fora do intervalo 0..100.</exception>
    public static Probability Create(int value)
    {
        if (value < 0 || value > 100)
            throw new ArgumentException(
                $"A probabilidade deve estar entre 0 e 100. Valor recebido: {value}.",
                nameof(value));

        return new Probability(value);
    }

    /// <inheritdoc/>
    public bool Equals(Probability? other)
    {
        if (other is null) return false;
        return Value == other.Value;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Probability other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => $"{Value}%";

    /// <summary>Operador de igualdade.</summary>
    public static bool operator ==(Probability? left, Probability? right)
        => left?.Equals(right) ?? right is null;

    /// <summary>Operador de desigualdade.</summary>
    public static bool operator !=(Probability? left, Probability? right)
        => !(left == right);
}
