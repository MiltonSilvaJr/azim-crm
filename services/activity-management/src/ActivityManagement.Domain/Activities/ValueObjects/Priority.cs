namespace ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Objeto de valor imutável que representa a prioridade de uma atividade comercial.
/// Valores aceitos: low, medium, high. Default: medium (Req 1.3).
/// Implementa igualdade por valor.
/// Mapeia: design §4.3, Req 1.3, TASK-02.
/// </summary>
public sealed class Priority : IEquatable<Priority>
{
    private static readonly HashSet<string> AllValues =
        new(["low", "medium", "high"], StringComparer.Ordinal);

    private Priority(string value) => Value = value;

    /// <summary>Valor textual da prioridade.</summary>
    public string Value { get; }

    /// <summary>Prioridade padrão quando não informada (Req 1.3).</summary>
    public static readonly Priority Default = new("medium");

    /// <summary>
    /// Cria uma <see cref="Priority"/> a partir do valor textual.
    /// </summary>
    /// <param name="value">Valor canônico da prioridade.</param>
    /// <returns>Instância imutável de <see cref="Priority"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Lançada quando <paramref name="value"/> não pertence à lista {low, medium, high}.
    /// </exception>
    public static Priority Create(string value)
    {
        if (!AllValues.Contains(value))
            throw new ArgumentException(
                $"Prioridade inválida: '{value}'. Valores aceitos: low, medium, high.", nameof(value));
        return new Priority(value);
    }

    // ── Igualdade por valor ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool Equals(Priority? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Priority other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Value);

    /// <summary>Igualdade estrutural entre dois objetos de valor.</summary>
    public static bool operator ==(Priority? left, Priority? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Desigualdade estrutural entre dois objetos de valor.</summary>
    public static bool operator !=(Priority? left, Priority? right) =>
        !(left == right);

    /// <inheritdoc/>
    public override string ToString() => Value;
}
