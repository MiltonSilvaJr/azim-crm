namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Snapshot leve do estágio lido de organization.
/// Armazena apenas os atributos necessários às decisões de negócio.
/// Mapeia: Req 5, Req 6, Req 7, design §4.3.
/// </summary>
public sealed record StageRef(
    Guid StageId,
    string Name,
    StageCategory Category,
    int DefaultProbability,
    int Order);
