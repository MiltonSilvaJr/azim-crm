namespace Digest.Infrastructure.Clock;

/// <summary>
/// Implementação de <see cref="IClock"/> que retorna o tempo real do sistema.
/// Registrada como <c>Singleton</c> no DI.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
