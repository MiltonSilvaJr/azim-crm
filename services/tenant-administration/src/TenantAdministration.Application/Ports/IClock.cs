namespace TenantAdministration.Application.Ports;

/// <summary>
/// Porta de entrada que abstrai o relógio do sistema para testabilidade.
/// Nunca use <c>DateTime.UtcNow</c> ou <c>DateTimeOffset.UtcNow</c> diretamente
/// em código de aplicação ou domínio.
/// Implementação concreta vem na Infrastructure via <c>SystemClock</c>.
/// </summary>
public interface IClock
{
    /// <summary>Retorna o instante atual em UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
