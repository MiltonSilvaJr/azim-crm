using DataMigration.Domain.ValueObjects;

namespace DataMigration.Domain.Policies;

/// <summary>
/// Resultado da aplicação da <see cref="StageFallbackPolicy"/>.
/// </summary>
/// <param name="StageName">Nome do estágio resultante (original ou "Lead").</param>
/// <param name="Flag">Flag de triagem gerada (não-bloqueante) ou <c>null</c>.</param>
public sealed record StageFallbackResult(string StageName, TriageFlag? Flag);

/// <summary>
/// Policy que aplica fallback de "Lead" para oportunidades com etapa vazia.
///
/// Nunca bloqueia o import; gera flag não-bloqueante <see cref="TriageFlagType.StageMissing"/>
/// para triagem assistida.
///
/// Rastreia: design §4.6, Req 5.1, TASK-07.
/// </summary>
public sealed class StageFallbackPolicy
{
    /// <summary>Nome do estágio de fallback quando etapa está vazia.</summary>
    public const string FallbackStageName = "Lead";

    /// <summary>
    /// Aplica o fallback de estágio.
    /// </summary>
    /// <param name="stageName">Nome do estágio da planilha (pode ser nulo ou vazio).</param>
    /// <param name="sourceRowIndex">Índice da linha para referência do flag (padrão 0).</param>
    public StageFallbackResult Apply(string? stageName, int sourceRowIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(stageName))
        {
            var flag = TriageFlag.ForRow(TriageFlagType.StageMissing, sourceRowIndex);
            return new StageFallbackResult(FallbackStageName, flag);
        }

        return new StageFallbackResult(stageName, Flag: null);
    }
}
