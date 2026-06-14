namespace ActivityManagement.Application.Ports;

/// <summary>
/// Port de relógio injetável para evitar dependência de <c>DateTimeOffset.UtcNow</c>
/// nos handlers da camada Application e no Domain (via parâmetro).
/// Implementado em Infrastructure; substituído por fake em testes.
/// Mapeia: design §5.4, RNF 6 (observabilidade temporal determinística).
/// </summary>
public interface IClock
{
    /// <summary>Retorna o instante corrente em UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
