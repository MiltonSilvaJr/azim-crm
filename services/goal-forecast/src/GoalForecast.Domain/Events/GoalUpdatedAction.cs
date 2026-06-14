namespace GoalForecast.Domain.Events;

/// <summary>
/// Discriminador da ação que originou o evento <see cref="GoalUpdated"/>.
/// Mapeia: design §4.4, payload canônico do goal.updated.v1.
/// </summary>
public enum GoalUpdatedAction
{
    /// <summary>Meta criada pela primeira vez.</summary>
    Created,

    /// <summary>ValorMeta atualizado em meta existente.</summary>
    Updated
}
