using Organization.Domain.Policies;
using Organization.Domain.ValueObjects;

namespace Organization.Domain.Aggregates;

/// <summary>
/// Fábrica de seed de estágios padrão conforme DD-002 (processo Vellus).
/// Produz exatamente 8 estágios: 6 open, 1 won (Ganho), 1 lost (Perdido).
/// Satisfaz PBT-06 e PBT-07 no estado inicial.
/// </summary>
public static class StageSeedFactory
{
    /// <summary>
    /// Retorna a lista canônica de estágios seed derivados do processo da Vellus (DD-002).
    /// Lead (10,open,1), Prospecção (25,open,2), Diagnóstico (50,open,3),
    /// Proposta Enviada (60,open,4), Negociação (75,open,5),
    /// Fechamento Provável (90,open,6), Ganho (100,won,7), Perdido (0,lost,8).
    /// </summary>
    public static IReadOnlyList<StageSeedEntry> CreateDefaultStages() =>
    [
        new("Lead",                Probability.Create(10),  StageCategory.Open, 1),
        new("Prospecção",          Probability.Create(25),  StageCategory.Open, 2),
        new("Diagnóstico",         Probability.Create(50),  StageCategory.Open, 3),
        new("Proposta Enviada",    Probability.Create(60),  StageCategory.Open, 4),
        new("Negociação",          Probability.Create(75),  StageCategory.Open, 5),
        new("Fechamento Provável", Probability.Create(90),  StageCategory.Open, 6),
        new("Ganho",               Probability.Hundred,     StageCategory.Won,  7),
        new("Perdido",             Probability.Zero,        StageCategory.Lost, 8),
    ];
}
