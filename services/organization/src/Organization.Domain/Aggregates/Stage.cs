using Organization.Domain.ValueObjects;

namespace Organization.Domain.Aggregates;

/// <summary>
/// Entidade interna do agregado <see cref="BusinessUnit"/> que representa um estágio do pipeline.
/// Pertence exclusivamente à sua BU; não tem identidade independente fora do contexto do agregado.
/// </summary>
public sealed class Stage
{
    /// <summary>Identificador único do estágio.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nome do estágio (único na BU, case-insensitive).</summary>
    public string Name { get; private set; }

    /// <summary>Probabilidade de fechamento (0..100).</summary>
    public Probability Probability { get; private set; }

    /// <summary>Categoria do estágio (open, won, lost).</summary>
    public StageCategory Category { get; private set; }

    /// <summary>Posição do estágio na ordenação do pipeline (único na BU).</summary>
    public int Position { get; private set; }

    private Stage() { Name = string.Empty; Probability = ValueObjects.Probability.Zero; Category = StageCategory.Open; }

    internal Stage(Guid id, string name, Probability probability, StageCategory category, int position)
    {
        Id = id;
        Name = name;
        Probability = probability;
        Category = category;
        Position = position;
    }

    internal void UpdatePosition(int newPosition)
    {
        Position = newPosition;
    }
}
