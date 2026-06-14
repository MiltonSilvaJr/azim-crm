namespace GoalForecast.Domain.ValueObjects;

/// <summary>
/// Discriminador do escopo de uma meta: BU (toda a unidade de negócio) ou
/// RESPONSAVEL (meta individual de um responsável dentro de uma BU).
/// Mapeia: Req 1, requirements §4, design §4.3, INV-3.
/// </summary>
public enum GoalScopeKind
{
    /// <summary>Meta para a unidade de negócio inteira. OwnerId é nulo.</summary>
    BU,

    /// <summary>Meta individual de um responsável vinculado a uma BU. OwnerId é obrigatório.</summary>
    RESPONSAVEL
}
