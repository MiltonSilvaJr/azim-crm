using GoalForecast.Domain.Exceptions;

namespace GoalForecast.Domain.ValueObjects;

/// <summary>
/// Objeto de valor imutável que representa o escopo de uma meta: BU ou RESPONSAVEL.
/// Protege a invariante INV-3: BuId é sempre obrigatório; OwnerId é obrigatório se e
/// somente se o escopo for RESPONSAVEL.
/// <see cref="Kind"/> é derivado da presença de <see cref="OwnerId"/>.
/// Igualdade por valor (record semantics).
///
/// Mapeia: Req 1.3, requirements §4, INV-3, design §4.3, TASK-04.
/// </summary>
public sealed record GoalScope
{
    /// <summary>Identificador da unidade de negócio. Sempre obrigatório (INV-3).</summary>
    public Guid BuId { get; }

    /// <summary>
    /// Identificador do responsável. Nulo no escopo BU; obrigatório no escopo RESPONSAVEL.
    /// </summary>
    public Guid? OwnerId { get; }

    /// <summary>
    /// Tipo do escopo: BU quando <see cref="OwnerId"/> é nulo; RESPONSAVEL caso contrário.
    /// </summary>
    public GoalScopeKind Kind => OwnerId.HasValue ? GoalScopeKind.RESPONSAVEL : GoalScopeKind.BU;

    private GoalScope(Guid buId, Guid? ownerId)
    {
        BuId = buId;
        OwnerId = ownerId;
    }

    /// <summary>
    /// Construtor sem validação para uso exclusivo do EF Core na materialização de entidades.
    /// Não deve ser chamado diretamente — use <see cref="ForBu"/> ou <see cref="ForResponsavel"/>.
    /// </summary>
#pragma warning disable CS8618
    private GoalScope() { }
#pragma warning restore CS8618

    /// <summary>
    /// Factory para escopo BU. OwnerId é nulo; Kind derivado como BU.
    /// </summary>
    /// <param name="buId">Identificador da BU. Não pode ser <see cref="Guid.Empty"/>.</param>
    /// <returns>Instância imutável de <see cref="GoalScope"/> do tipo BU.</returns>
    /// <exception cref="DomainException">GF-ERR-003 quando buId é vazio.</exception>
    public static GoalScope ForBu(Guid buId)
    {
        if (buId == Guid.Empty)
            throw new DomainException("GF-ERR-003",
                "Escopo inconsistente: BuId é obrigatório e não pode ser Guid.Empty (INV-3).");

        return new GoalScope(buId, ownerId: null);
    }

    /// <summary>
    /// Factory para escopo RESPONSAVEL. OwnerId é obrigatório; Kind derivado como RESPONSAVEL.
    /// </summary>
    /// <param name="buId">Identificador da BU. Não pode ser <see cref="Guid.Empty"/>.</param>
    /// <param name="ownerId">Identificador do responsável. Não pode ser <see cref="Guid.Empty"/>.</param>
    /// <returns>Instância imutável de <see cref="GoalScope"/> do tipo RESPONSAVEL.</returns>
    /// <exception cref="DomainException">GF-ERR-003 quando buId ou ownerId é vazio.</exception>
    public static GoalScope ForResponsavel(Guid buId, Guid ownerId)
    {
        if (buId == Guid.Empty)
            throw new DomainException("GF-ERR-003",
                "Escopo inconsistente: BuId é obrigatório e não pode ser Guid.Empty (INV-3).");

        if (ownerId == Guid.Empty)
            throw new DomainException("GF-ERR-003",
                "Escopo inconsistente: OwnerId é obrigatório no escopo RESPONSAVEL e não pode ser Guid.Empty (INV-3).");

        return new GoalScope(buId, ownerId);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        OwnerId.HasValue
            ? $"GoalScope(RESPONSAVEL, Bu={BuId}, Owner={OwnerId})"
            : $"GoalScope(BU, Bu={BuId})";
}
