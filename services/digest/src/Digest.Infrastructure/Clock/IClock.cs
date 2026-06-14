namespace Digest.Infrastructure.Clock;

/// <summary>
/// Abstração do relógio do sistema para permitir controle do tempo em testes (PBT-05, DD-004).
/// Implementação concreta retorna <see cref="DateTimeOffset.UtcNow"/>.
/// </summary>
public interface IClock
{
    /// <summary>Retorna o timestamp atual em UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
