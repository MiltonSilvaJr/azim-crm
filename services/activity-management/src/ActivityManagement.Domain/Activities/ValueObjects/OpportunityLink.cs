namespace ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Objeto de valor imutável que referencia uma oportunidade do mesmo tenant.
/// A existência e o ownership da oportunidade são validados pela porta
/// <c>IOpportunityReadPort</c> na camada de Application (Req 3.2).
/// Mapeia: design §4.3, Req 3.1, Req 3.2, TASK-02.
/// </summary>
public sealed class OpportunityLink : IEquatable<OpportunityLink>
{
    private OpportunityLink(Guid opportunityId) => OpportunityId = opportunityId;

    /// <summary>Identificador UUID da oportunidade vinculada.</summary>
    public Guid OpportunityId { get; }

    /// <summary>
    /// Cria um <see cref="OpportunityLink"/> a partir do identificador da oportunidade.
    /// </summary>
    /// <param name="opportunityId">UUID da oportunidade; não pode ser vazio.</param>
    /// <returns>Instância imutável de <see cref="OpportunityLink"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Lançada quando <paramref name="opportunityId"/> é <see cref="Guid.Empty"/>.
    /// </exception>
    public static OpportunityLink Create(Guid opportunityId)
    {
        if (opportunityId == Guid.Empty)
            throw new ArgumentException(
                "O identificador da oportunidade não pode ser vazio.", nameof(opportunityId));
        return new OpportunityLink(opportunityId);
    }

    // ── Igualdade por valor ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool Equals(OpportunityLink? other) =>
        other is not null && OpportunityId == other.OpportunityId;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is OpportunityLink other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(OpportunityId);

    /// <summary>Igualdade estrutural entre dois objetos de valor.</summary>
    public static bool operator ==(OpportunityLink? left, OpportunityLink? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Desigualdade estrutural entre dois objetos de valor.</summary>
    public static bool operator !=(OpportunityLink? left, OpportunityLink? right) =>
        !(left == right);

    /// <inheritdoc/>
    public override string ToString() => OpportunityId.ToString();
}
