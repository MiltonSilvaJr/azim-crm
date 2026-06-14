using DataMigration.Domain.ValueObjects;

namespace DataMigration.Domain.Policies;

/// <summary>
/// Policy que identifica pares de contas com mesmo <see cref="NormalizedName"/>
/// como candidatos a dedupe.
///
/// Resultado: grupos de nomes idênticos (após normalização).
/// Ação: não-bloqueante — a decisão de merge ou manutenção é humana (RN-014).
///
/// Rastreia: design §4.6, Req 7.1, 7.2, RN-014, TASK-07.
/// </summary>
public sealed class AccountDedupePolicy
{
    /// <summary>
    /// Encontra grupos de <see cref="NormalizedName"/> com o mesmo valor.
    /// Retorna apenas grupos com 2 ou mais membros.
    /// </summary>
    public IReadOnlyList<IGrouping<string, NormalizedName>> FindDuplicateCandidates(
        IEnumerable<NormalizedName> names)
    {
        return names
            .GroupBy(n => n.Value, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .ToList();
    }
}
