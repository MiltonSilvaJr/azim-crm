using TenantAdministration.Application.Ports;

namespace TenantAdministration.Infrastructure.Clock;

/// <summary>
/// Implementação concreta de <see cref="IClock"/> que usa o relógio do sistema.
/// Registrada como Singleton na DI para garantir instância única.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
