namespace AccountManagement.Application.Ports;

/// <summary>
/// Porta de saída que abstrai o acesso ao relógio do sistema.
///
/// Permite injeção de tempo controlado em testes (testabilidade).
/// A implementação concreta na Infrastructure retorna <see cref="DateTimeOffset.UtcNow"/>.
///
/// Mapeia: design §5 (Ports), regra clean-architecture.md §8 (sem DateTime.Now no domínio).
/// </summary>
public interface IClock
{
    /// <summary>Retorna o instante corrente em UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
