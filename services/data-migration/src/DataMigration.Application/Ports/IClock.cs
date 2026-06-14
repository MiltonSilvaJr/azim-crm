namespace DataMigration.Application.Ports;

/// <summary>
/// Porta de abstração de relógio para injeção de tempo nos handlers e behaviors.
///
/// Permite controle total de instantes em testes unitários (testabilidade).
///
/// Rastreia: design §5 (Application layer), regra de testabilidade do projeto.
/// </summary>
public interface IClock
{
    /// <summary>Retorna o instante atual em UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
