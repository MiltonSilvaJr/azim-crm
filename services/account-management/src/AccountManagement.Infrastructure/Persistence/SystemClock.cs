using AccountManagement.Application.Ports;

namespace AccountManagement.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IClock"/> que retorna o relógio do sistema.
///
/// Injeta <see cref="DateTimeOffset.UtcNow"/> em operações de domínio/aplicação
/// que precisam do tempo corrente, evitando o uso de <c>DateTime.Now</c> estático
/// diretamente (rule clean-architecture.md §8).
///
/// Mapeia: design §5 (Ports), IClock, TASK-10.
/// </summary>
internal sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
