namespace ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Objeto de valor imutável que representa o instante de vencimento de uma atividade.
/// Persiste como TIMESTAMPTZ (UTC). Base do cálculo de vencida e das faixas de visão.
/// Mapeia: design §4.3, Req 1.1, Req 5, Req 11, TASK-02.
/// </summary>
public sealed class DueDate : IEquatable<DueDate>
{
    private DueDate(DateTimeOffset value) => Value = value;

    /// <summary>Instante de vencimento da atividade.</summary>
    public DateTimeOffset Value { get; }

    /// <summary>
    /// Cria um <see cref="DueDate"/> a partir de um <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="value">Instante de vencimento.</param>
    /// <returns>Instância imutável de <see cref="DueDate"/>.</returns>
    public static DueDate Create(DateTimeOffset value) => new(value);

    // ── Igualdade por valor ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool Equals(DueDate? other) =>
        other is not null && Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DueDate other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Value);

    /// <summary>Igualdade estrutural entre dois objetos de valor.</summary>
    public static bool operator ==(DueDate? left, DueDate? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Desigualdade estrutural entre dois objetos de valor.</summary>
    public static bool operator !=(DueDate? left, DueDate? right) =>
        !(left == right);

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("O");
}
