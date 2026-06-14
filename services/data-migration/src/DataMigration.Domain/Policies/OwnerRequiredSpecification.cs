namespace DataMigration.Domain.Policies;

/// <summary>
/// Specification que verifica se todas as oportunidades triadas têm owner atribuído.
///
/// Verdadeira quando <c>ownerlessCandidateCount == 0</c>.
/// Guarda a transição para <c>importing</c> (PBT-07, design §4.5).
///
/// Extraída de <c>MigrationJob</c> como specification independente
/// (TASK-03 ST-03, design §4.6).
///
/// Rastreia: design §4.1, §4.5, §4.6, Req 4, PBT-07, TASK-03 ST-03.
/// </summary>
public static class OwnerRequiredSpecification
{
    /// <summary>
    /// Verifica se a specification é satisfeita.
    /// </summary>
    /// <param name="ownerlessCandidateCount">
    /// Número de oportunidades sem owner atribuído. Deve ser 0 para satisfazer.
    /// </param>
    /// <returns><c>true</c> quando todos têm owner; <c>false</c> caso contrário.</returns>
    public static bool IsSatisfied(int ownerlessCandidateCount) =>
        ownerlessCandidateCount == 0;
}
