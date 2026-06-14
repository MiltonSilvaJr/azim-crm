namespace ActivityManagement.Domain.Activities.ValueObjects;

using ActivityManagement.Domain.Activities.Exceptions;

/// <summary>
/// Objeto de valor imutável que representa o tipo de uma atividade comercial.
/// Valores aceitos: meeting, follow_up, call, email, task (lista canônica — Req 1.6).
/// Implementa igualdade por valor e rejeita qualquer valor fora da lista canônica.
/// Mapeia: design §4.3, Req 1.6, TASK-02.
/// </summary>
public sealed class ActivityType : IEquatable<ActivityType>
{
    /// <summary>Lista canônica de tipos aceitos (design §4.3, requirements §4.2).</summary>
    public static readonly IReadOnlyList<string> AllValues =
        ["meeting", "follow_up", "call", "email", "task"];

    private static readonly HashSet<string> AllValuesSet =
        new(AllValues, StringComparer.Ordinal);

    private ActivityType(string value) => Value = value;

    /// <summary>Valor textual do tipo de atividade.</summary>
    public string Value { get; }

    /// <summary>
    /// Cria um <see cref="ActivityType"/> a partir do valor textual.
    /// </summary>
    /// <param name="value">Valor canônico do tipo de atividade.</param>
    /// <returns>Instância imutável de <see cref="ActivityType"/>.</returns>
    /// <exception cref="InvalidActivityTypeException">
    /// Lançada quando <paramref name="value"/> não pertence à lista canônica.
    /// </exception>
    public static ActivityType Create(string value)
    {
        if (!AllValuesSet.Contains(value))
            throw new InvalidActivityTypeException(value);
        return new ActivityType(value);
    }

    // ── Igualdade por valor ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool Equals(ActivityType? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ActivityType other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Value);

    /// <summary>Igualdade estrutural entre dois objetos de valor.</summary>
    public static bool operator ==(ActivityType? left, ActivityType? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Desigualdade estrutural entre dois objetos de valor.</summary>
    public static bool operator !=(ActivityType? left, ActivityType? right) =>
        !(left == right);

    /// <inheritdoc/>
    public override string ToString() => Value;
}
