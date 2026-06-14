namespace ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Objeto de valor imutável que encapsula o status de uma atividade e sua máquina de estados.
/// Valores: pending, in_progress, completed (terminal), cancelled (terminal).
/// Transições válidas (design §4.5, Req 4):
///   pending     → in_progress, completed, cancelled
///   in_progress → pending, completed, cancelled
///   completed   → (nenhuma)
///   cancelled   → (nenhuma)
/// Mapeia: design §4.3, §4.5, Req 4, PBT-01, TASK-02.
/// </summary>
public sealed class ActivityStatus : IEquatable<ActivityStatus>
{
    private static readonly HashSet<string> AllValuesSet =
        new(["pending", "in_progress", "completed", "cancelled"], StringComparer.Ordinal);

    // Mapa de transições válidas conforme design §4.5
    private static readonly Dictionary<string, HashSet<string>> Transitions = new()
    {
        ["pending"]     = new(["in_progress", "completed", "cancelled"], StringComparer.Ordinal),
        ["in_progress"] = new(["pending", "completed", "cancelled"],     StringComparer.Ordinal),
        ["completed"]   = [],
        ["cancelled"]   = [],
    };

    private ActivityStatus(string value) => Value = value;

    // ── Instâncias canônicas ─────────────────────────────────────────────────

    /// <summary>Status inicial de toda atividade criada (I3).</summary>
    public static readonly ActivityStatus Pending     = new("pending");

    /// <summary>Atividade em andamento.</summary>
    public static readonly ActivityStatus InProgress  = new("in_progress");

    /// <summary>Atividade concluída (terminal — I5).</summary>
    public static readonly ActivityStatus Completed   = new("completed");

    /// <summary>Atividade cancelada (terminal).</summary>
    public static readonly ActivityStatus Cancelled   = new("cancelled");

    // ── Propriedades ─────────────────────────────────────────────────────────

    /// <summary>Valor textual do status.</summary>
    public string Value { get; }

    /// <summary>
    /// Indica se o status é terminal (completed ou cancelled).
    /// Atividade terminal rejeita qualquer transição de saída (I6, design §4.5).
    /// </summary>
    public bool IsTerminal =>
        string.Equals(Value, "completed", StringComparison.Ordinal) ||
        string.Equals(Value, "cancelled",  StringComparison.Ordinal);

    // ── Fábrica ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria um <see cref="ActivityStatus"/> a partir do valor textual.
    /// </summary>
    /// <param name="value">Valor canônico do status.</param>
    /// <returns>Instância imutável de <see cref="ActivityStatus"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Lançada quando <paramref name="value"/> não pertence ao conjunto canônico.
    /// </exception>
    public static ActivityStatus Create(string value)
    {
        if (!AllValuesSet.Contains(value))
            throw new ArgumentException(
                $"Status inválido: '{value}'. Valores aceitos: pending, in_progress, completed, cancelled.",
                nameof(value));

        return value switch
        {
            "pending"     => Pending,
            "in_progress" => InProgress,
            "completed"   => Completed,
            "cancelled"   => Cancelled,
            _             => new ActivityStatus(value),
        };
    }

    /// <summary>
    /// Verifica se o valor textual pertence ao conjunto canônico de status.
    /// Usado pelo PBT-01 para filtragem de geradores.
    /// </summary>
    /// <param name="value">Valor a verificar.</param>
    /// <returns><c>true</c> quando o valor é canônico.</returns>
    public static bool IsValidValue(string value) => AllValuesSet.Contains(value);

    // ── Máquina de estados ───────────────────────────────────────────────────

    /// <summary>
    /// Verifica se a transição do status corrente para <paramref name="target"/> é permitida
    /// pela máquina de estados (design §4.5, Req 4.2).
    /// </summary>
    /// <param name="target">Status alvo da transição.</param>
    /// <returns>
    /// <c>true</c> quando a transição é válida; <c>false</c> quando é proibida ou o
    /// status corrente é terminal.
    /// </returns>
    public bool CanTransitionTo(ActivityStatus target) =>
        Transitions[Value].Contains(target.Value);

    // ── Igualdade por valor ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool Equals(ActivityStatus? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ActivityStatus other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Value);

    /// <summary>Igualdade estrutural entre dois objetos de valor.</summary>
    public static bool operator ==(ActivityStatus? left, ActivityStatus? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Desigualdade estrutural entre dois objetos de valor.</summary>
    public static bool operator !=(ActivityStatus? left, ActivityStatus? right) =>
        !(left == right);

    /// <inheritdoc/>
    public override string ToString() => Value;
}
