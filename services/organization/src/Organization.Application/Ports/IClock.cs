namespace Organization.Application.Ports;

/// <summary>
/// Port de saída para obtenção do instante de tempo atual.
/// Abstrai <c>DateTimeOffset.UtcNow</c> para permitir teste determinístico.
/// Implementado pela Infrastructure; nunca use <c>DateTimeOffset.UtcNow</c> diretamente nos handlers.
/// </summary>
public interface IClock
{
    /// <summary>Retorna o instante atual em UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
