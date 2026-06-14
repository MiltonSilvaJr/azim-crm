using GoalForecast.Domain.Authorization;

namespace GoalForecast.Application.Behaviors;

/// <summary>
/// Marca um command/query que carrega o <see cref="GoalPrincipal"/> autenticado.
/// Usado pelos behaviors para extrair tenant_id, bu_id e papel sem depender de HttpContext.
/// Mapeia: design §5.4, Req 12.1, TASK-10.
/// </summary>
public interface IHasPrincipal
{
    /// <summary>Principal autenticado extraído do JWT pelo controller/endpoint.</summary>
    GoalPrincipal Principal { get; }
}
