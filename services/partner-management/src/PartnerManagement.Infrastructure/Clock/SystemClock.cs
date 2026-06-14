using PartnerManagement.Application.Ports;

namespace PartnerManagement.Infrastructure.Clock;

/// <summary>
/// Implementação de <see cref="IClock"/> que retorna <c>DateTimeOffset.UtcNow</c>.
/// Registrada como Singleton no DI.
/// Permite substituição por clock controlado em testes sem depender de tempo real.
/// Mapeia: design §5.4, TASK-20.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
