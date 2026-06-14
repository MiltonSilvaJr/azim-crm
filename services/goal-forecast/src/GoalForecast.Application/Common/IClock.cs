namespace GoalForecast.Application.Common;

/// <summary>
/// Porta de tempo abstrata para testabilidade de operações dependentes de data/hora.
/// Permite controle de <see cref="DateTimeOffset.UtcNow"/> em testes.
/// Mapeia: design §5, regra de testabilidade (tasks.md §1.1).
/// </summary>
public interface IClock
{
    /// <summary>Retorna o instante atual em UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
