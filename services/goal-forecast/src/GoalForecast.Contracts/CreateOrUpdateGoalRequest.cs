namespace GoalForecast.Contracts;

/// <summary>
/// Request de criação ou atualização de meta (upsert).
/// TenantId e Id nunca são aceitos no body — proteção de over-posting (Req 12.1, design §10).
/// Todos os valores monetários em centavos inteiros (RNF 4).
///
/// Mapeia: design §8.1, §8.2, TASK-21.
/// </summary>
public sealed record CreateOrUpdateGoalRequest
{
    /// <summary>
    /// Escopo da meta: "BU" ou "RESPONSAVEL".
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Identificador da unidade de negócio. Obrigatório.
    /// </summary>
    public required Guid BuId { get; init; }

    /// <summary>
    /// Identificador do responsável. Obrigatório em escopo RESPONSAVEL; nulo em BU.
    /// </summary>
    public Guid? OwnerId { get; init; }

    /// <summary>Ano do período. Quatro dígitos.</summary>
    public required int Year { get; init; }

    /// <summary>Mês do período. 1..12.</summary>
    public required int Month { get; init; }

    /// <summary>
    /// Valor da meta em centavos inteiros não-negativos (RNF 4, DEC-011).
    /// Nunca double, nunca decimal.
    /// </summary>
    public required long ValorMeta { get; init; }
}
