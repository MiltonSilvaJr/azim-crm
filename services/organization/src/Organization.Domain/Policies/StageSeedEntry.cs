using Organization.Domain.ValueObjects;

namespace Organization.Domain.Policies;

/// <summary>
/// Entrada de seed de estágio produzida pela <see cref="StageSeedFactory"/>.
/// Imutável; contém os dados para criação do estágio inicial.
/// </summary>
public sealed record StageSeedEntry(
    string Name,
    Probability Probability,
    StageCategory Category,
    int Position);
